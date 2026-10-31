using Construction.Kernel;
using Construction.Platform.Core.Application.Outbox;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Construction.Platform.Infrastructure.Application;

/// <summary>
/// Tenant-scoped PostgreSQL operations for dead letters, replay/discard and outbox lag.
/// Every statement runs inside a transaction that sets <c>app.tenant_id</c>, so the forced RLS policy
/// on <c>platform.outbox_dead</c> / <c>platform.outbox</c> applies exactly as it does for requests.
/// </summary>
public sealed class DeadLetterService : IDeadLetterStore, IOutboxMetricsStore
{
    public const int MaxNoteLength = 500;

    private const string DeadColumns = """
        id, original_event_id, tenant_id, type, payload::text, aggregate_type, aggregate_id,
        aggregate_version, attempts, last_error, last_error_category, error_class, dead_at,
        resolution, resolved_by, resolved_at, resolution_note, replayed_at
        """;

    private readonly string _connectionString;
    private readonly IClock _clock;

    public DeadLetterService(IConfiguration configuration, IClock clock)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IReadOnlyList<DeadOutboxMessageDto>> ListAsync(
        Guid tenantId,
        string? eventType,
        string? resolution,
        DateTimeOffset? cursorDeadAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }
        if (cursorDeadAt.HasValue != cursorId.HasValue)
        {
            throw new ArgumentException("Both cursor values must be supplied together.");
        }
        if (resolution is not null && !IsKnownResolution(resolution))
        {
            throw new ArgumentException("Unknown resolution filter.", nameof(resolution));
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {DeadColumns}
            FROM platform.outbox_dead
            WHERE tenant_id = @tenantId
              AND (@eventType IS NULL OR type = @eventType)
              AND (@resolution IS NULL OR resolution = @resolution)
              AND (@cursorDeadAt IS NULL OR (dead_at, id) < (@cursorDeadAt, @cursorId))
            ORDER BY dead_at DESC, id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("eventType", (object?)eventType ?? DBNull.Value);
        command.Parameters.AddWithValue("resolution", (object?)resolution ?? DBNull.Value);
        command.Parameters.AddWithValue("cursorDeadAt", (object?)cursorDeadAt ?? DBNull.Value);
        command.Parameters.AddWithValue("cursorId", (object?)cursorId ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", pageSize + 1);

        var rows = new List<DeadOutboxMessageDto>(pageSize + 1);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadRow(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rows;
    }

    public async Task<DeadOutboxMessageDto?> GetAsync(
        Guid tenantId,
        Guid deadMessageId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        if (deadMessageId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {DeadColumns}
            FROM platform.outbox_dead
            WHERE id = @id AND tenant_id = @tenantId;
            """;
        command.Parameters.AddWithValue("id", deadMessageId);
        command.Parameters.AddWithValue("tenantId", tenantId);

        DeadOutboxMessageDto? result = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                result = ReadRow(reader);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<DeadLetterMutationResult> ReplayAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        EnsureActor(actorId);
        var trimmed = EnsureReason(reason, nameof(reason));

        var now = _clock.UtcNow;
        var newEventId = Guid.CreateVersion7();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        // Conditional transition: the second concurrent click updates 0 rows and is reported as 409.
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH replayed AS (
                UPDATE platform.outbox_dead
                SET resolution = 'replayed',
                    resolved_by = @actorId,
                    resolved_at = @now,
                    resolution_note = @reason,
                    replayed_at = @now,
                    replayed_by = @actorId,
                    replay_reason = @reason
                WHERE id = @deadId AND tenant_id = @tenantId AND resolution = 'open'
                RETURNING tenant_id, type, payload, aggregate_type, aggregate_id, aggregate_version, original_event_id
            )
            INSERT INTO platform.outbox
                (id, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload,
                 occurred_at, attempts, next_attempt_at, locked_by, locked_until, dispatched_at, dead_at)
            SELECT @newEventId, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload,
                   @now, 0, @now, NULL, NULL, NULL, NULL
            FROM replayed
            RETURNING id;
            """;
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("actorId", actorId);
        command.Parameters.AddWithValue("reason", trimmed);
        command.Parameters.AddWithValue("deadId", deadMessageId);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("newEventId", newEventId);

        var inserted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (inserted is null)
        {
            var existing = await ExistsAsync(connection, transaction, tenantId, deadMessageId, cancellationToken)
                .ConfigureAwait(false);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return existing
                ? DeadLetterMutationResult.AlreadyResolved(deadMessageId)
                : DeadLetterMutationResult.NotFound();
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new DeadLetterMutationResult(DeadLetterResolutionState.Resolved, newEventId, now);
    }

    public async Task<DeadLetterMutationResult> DiscardAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string note,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        EnsureActor(actorId);
        var trimmed = EnsureReason(note, nameof(note));

        var now = _clock.UtcNow;

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE platform.outbox_dead
            SET resolution = 'discarded',
                resolved_by = @actorId,
                resolved_at = @now,
                resolution_note = @note
            WHERE id = @deadId AND tenant_id = @tenantId AND resolution = 'open'
            RETURNING id;
            """;
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("actorId", actorId);
        command.Parameters.AddWithValue("note", trimmed);
        command.Parameters.AddWithValue("deadId", deadMessageId);
        command.Parameters.AddWithValue("tenantId", tenantId);

        var updated = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (updated is null)
        {
            var existing = await ExistsAsync(connection, transaction, tenantId, deadMessageId, cancellationToken)
                .ConfigureAwait(false);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return existing
                ? DeadLetterMutationResult.AlreadyResolved(deadMessageId)
                : DeadLetterMutationResult.NotFound();
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new DeadLetterMutationResult(DeadLetterResolutionState.Resolved, deadMessageId, now);
    }

    public async Task<OutboxLagDto> GetLagAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COALESCE(EXTRACT(EPOCH FROM (now() - MIN(occurred_at))), 0) AS lag_seconds,
                   COUNT(*) AS pending_count
            FROM platform.outbox
            WHERE tenant_id = @tenantId AND dispatched_at IS NULL AND dead_at IS NULL;
            """;
        command.Parameters.AddWithValue("tenantId", tenantId);

        double lag = 0;
        long pending = 0;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lag = Convert.ToDouble(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
                pending = Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new OutboxLagDto(Math.Max(0, lag), pending);
    }

    private static async Task<bool> ExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid deadMessageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM platform.outbox_dead WHERE id = @id AND tenant_id = @tenantId LIMIT 1;";
        command.Parameters.AddWithValue("id", deadMessageId);
        command.Parameters.AddWithValue("tenantId", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
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

    private static string EnsureReason(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxNoteLength)
        {
            throw new ArgumentException(
                $"A reason of 1 to {MaxNoteLength} characters is required.",
                parameterName);
        }

        return value.Trim();
    }

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required.", nameof(actorId));
        }
    }

    private static bool IsKnownResolution(string value) =>
        value is "open" or "replayed" or "discarded";

    private static DeadOutboxMessageDto ReadRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetGuid(6),
        reader.GetInt64(7),
        reader.GetInt32(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.GetFieldValue<DateTimeOffset>(12),
        reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetGuid(14),
        reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
        reader.IsDBNull(16) ? null : reader.GetString(16),
        reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17));
}
