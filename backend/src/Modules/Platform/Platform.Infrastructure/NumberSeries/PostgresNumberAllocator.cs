using System.Data;
using System.Data.Common;
using System.Globalization;
using Construction.Kernel;
using Construction.Platform.Core.Application.NumberSeries;
using Construction.Platform.Core.Domain.NumberSeries;
using Npgsql;

namespace Construction.Platform.Infrastructure.NumberSeries;

/// <summary>
/// Gapless allocator (spec 01 §4.9). Every statement runs on the caller's connection and transaction:
/// the counter row lock is held until the caller commits, so a rollback reissues the number
/// (BR-PLT-20). A unique violation on the allocation is treated as a defect (BR-PLT-24).
/// </summary>
public sealed class PostgresNumberAllocator : INumberAllocator
{
    private const string LockTimeoutSql = "SET LOCAL lock_timeout = '5s'";

    public async Task<AllocatedNumber> AllocateAsync(
        DbConnection connection,
        DbTransaction transaction,
        NumberRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                "Number allocation requires an open caller connection; call it inside the caller's transaction.");
        }

        var series = await LoadSeriesAsync(connection, transaction, request.SeriesCode, cancellationToken)
            .ConfigureAwait(false);

        var documentDate = request.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var atUtc = new DateTimeOffset(
            documentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), TimeSpan.Zero);

        var scopeKey = NumberSeriesRules.ScopeKey(
            series.Scope, request.ProjectId, request.ProjectCode, atUtc, request.TenantTimeZoneId);

        if (scopeKey.IsFailure)
        {
            throw new DomainRuleViolation(scopeKey.Error.Code, scopeKey.Error.Message);
        }

        try
        {
            await using (var timeout = CreateCommand(connection, transaction, LockTimeoutSql))
            {
                await timeout.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var ensure = CreateCommand(connection, transaction, """
                INSERT INTO platform.number_series_counters (id, tenant_id, series_id, scope_key, next_value)
                SELECT @id, s.tenant_id, s.id, @scopeKey, s.start_value
                FROM platform.number_series s
                WHERE s.code = @code
                ON CONFLICT (series_id, scope_key) DO NOTHING;
                """))
            {
                Add(ensure, "id", Guid.CreateVersion7());
                Add(ensure, "scopeKey", scopeKey.Value);
                Add(ensure, "code", series.Code);
                await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            long issued;
            await using (var take = CreateCommand(connection, transaction, """
                UPDATE platform.number_series_counters c
                SET next_value = c.next_value + s.step
                FROM platform.number_series s
                WHERE c.series_id = s.id AND s.code = @code AND c.scope_key = @scopeKey
                RETURNING c.next_value - s.step;
                """))
            {
                Add(take, "code", series.Code);
                Add(take, "scopeKey", scopeKey.Value);
                var value = await take.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (value is null or DBNull)
                {
                    throw new DomainRuleViolation(
                        "platform.number_series_not_found",
                        "The number series was not found.");
                }

                issued = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            var yearText = NumberSeriesRules.YearInTimeZone(atUtc, request.TenantTimeZoneId);
            var number = NumberSeriesRules.Render(
                series.Format,
                series.Prefix,
                issued,
                series.Padding,
                yearText,
                request.ProjectCode);

            await using (var insert = CreateCommand(connection, transaction, """
                INSERT INTO platform.number_allocations
                    (id, tenant_id, series_id, scope_key, seq_value, number, entity_type, entity_id, allocated_at)
                VALUES
                    (@id, @tenantId, @seriesId, @scopeKey, @seq, @number, @entityType, @entityId, now());
                """))
            {
                Add(insert, "id", Guid.CreateVersion7());
                Add(insert, "tenantId", series.TenantId);
                Add(insert, "seriesId", series.Id);
                Add(insert, "scopeKey", scopeKey.Value);
                Add(insert, "seq", issued);
                Add(insert, "number", number);
                Add(insert, "entityType", (object?)request.EntityType ?? DBNull.Value);
                Add(insert, "entityId", (object?)request.EntityId ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var lockSeries = CreateCommand(connection, transaction, """
                UPDATE platform.number_series SET is_locked = true
                WHERE id = @id AND NOT is_locked;
                """))
            {
                Add(lockSeries, "id", series.Id);
                await lockSeries.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return new AllocatedNumber(number, issued, series.Code, scopeKey.Value);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainRuleViolation(
                "platform.number_collision",
                "A duplicate document number was detected.",
                ex);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new DomainRuleViolation(
                "platform.number_busy",
                "The number series is busy; retry the operation.",
                ex);
        }
    }

    public async Task<NumberPreview> PreviewAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string seriesCode,
        Guid? projectId,
        string? projectCode,
        DateOnly? documentDate,
        string? tenantTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesCode);

        var series = await LoadSeriesAsync(connection, transaction, seriesCode, cancellationToken)
            .ConfigureAwait(false);

        var date = documentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var atUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), TimeSpan.Zero);
        var scopeKey = NumberSeriesRules.ScopeKey(series.Scope, projectId, projectCode, atUtc, tenantTimeZoneId);

        if (scopeKey.IsFailure)
        {
            throw new DomainRuleViolation(scopeKey.Error.Code, scopeKey.Error.Message);
        }

        long next;
        await using (var read = CreateCommand(connection, transaction, """
            SELECT next_value FROM platform.number_series_counters
            WHERE series_id = @seriesId AND scope_key = @scopeKey;
            """))
        {
            Add(read, "seriesId", series.Id);
            Add(read, "scopeKey", scopeKey.Value);
            var value = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            next = value is null or DBNull
                ? series.StartValue
                : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        var number = NumberSeriesRules.Render(
            series.Format,
            series.Prefix,
            next,
            series.Padding,
            NumberSeriesRules.YearInTimeZone(atUtc, tenantTimeZoneId),
            projectCode);

        return new NumberPreview(series.Code, number, scopeKey.Value);
    }

    private static async Task<SeriesRow> LoadSeriesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, """
            SELECT id, tenant_id, code, prefix, padding, format, scope, start_value, is_locked, step
            FROM platform.number_series
            WHERE code = @code;
            """);
        Add(command, "code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new DomainRuleViolation("platform.number_series_not_found", "The number series was not found.");
        }

        var scopeText = reader.GetString(6);
        var scope = Enum.TryParse<NumberScope>(scopeText, ignoreCase: true, out var parsed)
            ? parsed
            : NumberScope.Tenant;

        return new SeriesRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5),
            scope,
            reader.GetInt64(7),
            reader.GetBoolean(8));
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? transaction,
        string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Add(DbCommand command, string name, object value) =>
        command.Parameters.Add(new NpgsqlParameter(name, value));

    private sealed record SeriesRow(
        Guid Id,
        Guid TenantId,
        string Code,
        string Prefix,
        int Padding,
        string Format,
        NumberScope Scope,
        long StartValue,
        bool IsLocked);
}
