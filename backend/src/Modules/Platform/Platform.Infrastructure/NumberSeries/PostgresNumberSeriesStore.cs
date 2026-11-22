using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Audit;
using Construction.Platform.Core.Application.NumberSeries;
using Construction.Platform.Core.Domain.NumberSeries;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Construction.Platform.Infrastructure.NumberSeries;

/// <summary>
/// Tenant-scoped number-series reads and locked updates. Every statement runs inside a transaction
/// that sets <c>app.tenant_id</c>, so forced RLS applies.
/// </summary>
public sealed class PostgresNumberSeriesStore : INumberSeriesStore
{
    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLog _auditLog;

    public PostgresNumberSeriesStore(
        IConfiguration configuration,
        ITenantContext tenantContext,
        IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    public async Task<IReadOnlyList<NumberSeriesDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.code, s.name_ar, s.name_en, s.prefix, s.padding, s.scope, s.format,
                   s.start_value, s.is_locked, s.row_version,
                   (SELECT c.next_value FROM platform.number_series_counters c
                    WHERE c.series_id = s.id AND c.scope_key = '-')
            FROM platform.number_series s
            ORDER BY s.code;
            """;

        var items = new List<NumberSeriesDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var scope = reader.GetString(5);
                long? next = reader.IsDBNull(10) ? null : reader.GetInt64(10);
                items.Add(ToDto(reader, next, scope));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return items;
    }

    public async Task<NumberSeriesDto?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var dto = await ReadSeriesAsync(connection, transaction, code, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async Task<Result<NumberSeriesDto>> UpdateAsync(
        string code,
        UpdateNumberSeriesRequest request,
        int expectedRowVersion,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var current = await ReadSeriesAsync(connection, transaction, code, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<NumberSeriesDto>(Error.NotFound(
                "platform.number_series_not_found", "The number series was not found."));
        }

        if (current.RowVersion != expectedRowVersion)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<NumberSeriesDto>(Error.PreconditionFailed(
                "platform.precondition_failed",
                "The number series was modified by another request."));
        }

        var lastIssued = await LastIssuedAsync(connection, transaction, code, cancellationToken)
            .ConfigureAwait(false);

        var definition = new NumberSeriesDefinition(
            current.Code,
            current.Prefix,
            ParseScope(current.Scope),
            current.Format,
            current.Padding,
            current.StartValue,
            current.IsLocked,
            lastIssued);

        var change = new NumberSeriesChange(
            Format: request.Format,
            Padding: request.Padding,
            StartValue: request.StartAt);

        var error = NumberSeriesRules.ValidateChange(definition, change);
        if (error is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<NumberSeriesDto>(error);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE platform.number_series
                SET format = COALESCE(@format, format),
                    padding = COALESCE(@padding, padding),
                    start_value = COALESCE(@startAt, start_value),
                    row_version = row_version + 1,
                    updated_at = now()
                WHERE code = @code;
                """;
            update.Parameters.Add(new NpgsqlParameter("format", (object?)request.Format ?? DBNull.Value));
            update.Parameters.Add(new NpgsqlParameter("padding", (object?)request.Padding ?? DBNull.Value));
            update.Parameters.Add(new NpgsqlParameter("startAt", (object?)request.StartAt ?? DBNull.Value));
            update.Parameters.Add(new NpgsqlParameter("code", code));
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (request.StartAt is { } startAt)
        {
            await using var counters = connection.CreateCommand();
            counters.Transaction = transaction;
            counters.CommandText = """
                UPDATE platform.number_series_counters c
                SET next_value = GREATEST(c.next_value, @startAt)
                FROM platform.number_series s
                WHERE c.series_id = s.id AND s.code = @code;
                """;
            counters.Parameters.Add(new NpgsqlParameter("startAt", startAt));
            counters.Parameters.Add(new NpgsqlParameter("code", code));
            await counters.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var seriesStart = connection.CreateCommand();
            seriesStart.Transaction = transaction;
            seriesStart.CommandText = """
                UPDATE platform.number_series SET start_value = @startAt WHERE code = @code;
                """;
            seriesStart.Parameters.Add(new NpgsqlParameter("startAt", startAt));
            seriesStart.Parameters.Add(new NpgsqlParameter("code", code));
            await seriesStart.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var updated = await ReadSeriesAsync(connection, transaction, code, cancellationToken).ConfigureAwait(false);

        await _auditLog.WriteAsync(
            connection,
            transaction,
            new AuditEntry(
                tenantId,
                actorId == Guid.Empty ? null : actorId,
                "number_series",
                null,
                "number_series.updated",
                Before: JsonSerializer.Serialize(new
                {
                    current.Format,
                    current.Padding,
                    current.StartValue,
                    current.IsLocked
                }),
                After: JsonSerializer.Serialize(new
                {
                    updated!.Format,
                    updated.Padding,
                    updated.StartValue,
                    updated.IsLocked
                }),
                Reason: request.Reason),
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(updated!);
    }

    public async Task<NumberAllocationPage> ListAllocationsAsync(
        string code,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset? cursorAllocatedAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT a.id, a.number, a.seq_value, a.scope_key, a.entity_type, a.entity_id, a.allocated_at
            FROM platform.number_allocations a
            JOIN platform.number_series s ON s.id = a.series_id
            WHERE s.code = @code
              AND (@from IS NULL OR a.allocated_at >= @from)
              AND (@to IS NULL OR a.allocated_at <= @to)
              AND (@cursorAt IS NULL OR (a.allocated_at, a.id) < (@cursorAt, @cursorId))
            ORDER BY a.allocated_at DESC, a.id DESC
            LIMIT @limit;
            """;
        command.Parameters.Add(new NpgsqlParameter("code", code));
        command.Parameters.Add(new NpgsqlParameter("from", (object?)from ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("to", (object?)to ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("cursorAt", (object?)cursorAllocatedAt ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("cursorId", (object?)cursorId ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("limit", pageSize + 1));

        var items = new List<NumberAllocationDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new NumberAllocationDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    reader.GetFieldValue<DateTimeOffset>(6)));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var hasMore = items.Count > pageSize;
        var page = items.Take(pageSize).ToArray();

        return new NumberAllocationPage(
            page,
            hasMore,
            hasMore && page.Length > 0 ? page[^1].AllocatedAt : null,
            hasMore && page.Length > 0 ? page[^1].Id : null);
    }

    public async Task<int> EnsureDefaultsAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO platform.number_series
                (id, tenant_id, code, name_ar, name_en, prefix, padding, scope, format, start_value)
            SELECT gen_random_uuid(), current_setting('app.tenant_id')::uuid,
                   v.code, v.name_ar, v.name_en, v.prefix, v.padding, v.scope, v.format, 1
            FROM (VALUES
                ('PO',  'أمر شراء',              'Purchase Order',              'PO',  4, 'tenant',  '{prefix}-{seq}'),
                ('PR',  'طلب شراء',              'Purchase Request',            'PR',  4, 'tenant',  '{prefix}-{seq}'),
                ('VO',  'أمر تغيير',             'Variation Order',             'VO',  2, 'tenant',  '{prefix}-{seq}'),
                ('PA',  'طلب دفع',               'Payment Application',         'PA',  2, 'project', '{prefix}-{seq}'),
                ('RFQ', 'طلب عرض سعر',           'Request for Quotation',       'RFQ', 4, 'tenant',  '{prefix}-{seq}'),
                ('GRN', 'إذن استلام',            'Goods Receipt Note',          'GRN', 4, 'tenant',  '{prefix}-{seq}'),
                ('INV', 'فاتورة',                'Invoice',                     'INV', 4, 'tenant',  '{prefix}-{seq}'),
                ('NCR', 'تقرير عدم مطابقة',      'Non-Conformance Report',      'NCR', 4, 'tenant',  '{prefix}-{seq}'),
                ('RFI', 'طلب معلومات',           'Request for Information',     'RFI', 4, 'tenant',  '{prefix}-{seq}'),
                ('TN',  'مذكرة فنية',            'Technical Note',              'TN',  4, 'tenant',  '{prefix}-{seq}'),
                ('EST', 'تقدير',                 'Estimate',                    'EST', 4, 'tenant',  '{prefix}-{seq}'),
                ('SUB', 'مقاول باطن',            'Subcontract',                 'SUB', 4, 'tenant',  '{prefix}-{seq}'),
                ('VAL', 'مستخلص',                'Valuation',                   'VAL', 4, 'tenant',  '{prefix}-{seq}'),
                ('TASK','مهمة',                  'Task',                        'TASK',4, 'tenant',  '{prefix}-{seq}'),
                ('IMP', 'استيراد',               'Import',                      'IMP', 4, 'tenant',  '{prefix}-{seq}')
            ) AS v(code, name_ar, name_en, prefix, padding, scope, format)
            ON CONFLICT (tenant_id, code) DO NOTHING;
            """;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NumberSeriesDto?> ReadSeriesAsync(
        DbConnection connection,
        DbTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.code, s.name_ar, s.name_en, s.prefix, s.padding, s.scope, s.format,
                   s.start_value, s.is_locked, s.row_version,
                   (SELECT c.next_value FROM platform.number_series_counters c
                    WHERE c.series_id = s.id AND c.scope_key = '-')
            FROM platform.number_series s
            WHERE s.code = @code;
            """;
        command.Parameters.Add(new NpgsqlParameter("code", code));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var scope = reader.GetString(5);
        long? next = reader.IsDBNull(10) ? null : reader.GetInt64(10);
        return ToDto(reader, next, scope);
    }

    private static async Task<long> LastIssuedAsync(
        DbConnection connection,
        DbTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COALESCE(MAX(a.seq_value), 0)
            FROM platform.number_allocations a
            JOIN platform.number_series s ON s.id = a.series_id
            WHERE s.code = @code;
            """;
        command.Parameters.Add(new NpgsqlParameter("code", code));

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static NumberSeriesDto ToDto(DbDataReader reader, long? next, string scope)
    {
        var prefix = reader.GetString(3);
        var padding = reader.GetInt32(4);
        var format = reader.GetString(6);
        var start = reader.GetInt64(7);

        var preview = string.Equals(scope, "tenant", StringComparison.Ordinal)
            ? NumberSeriesRules.Render(
                format,
                prefix,
                next ?? start,
                padding,
                DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture),
                null)
            : null;

        return new NumberSeriesDto(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            prefix,
            padding,
            scope,
            format,
            start,
            reader.GetBoolean(8),
            reader.GetInt32(9),
            preview);
    }

    private static NumberScope ParseScope(string scope) =>
        scope switch
        {
            "project" => NumberScope.Project,
            "year" => NumberScope.Year,
            "project_year" => NumberScope.ProjectYear,
            _ => NumberScope.Tenant
        };

    private Guid CurrentTenant()
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("A tenant context is required.");
        }

        return _tenantContext.Current.Value;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
        command.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
