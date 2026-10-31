using Construction.Kernel;
using Construction.Platform.Core.Application.Outbox;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace Construction.Platform.Infrastructure.Application;

/// <summary>Tenant-scoped PostgreSQL operations for dead letters and atomic replay.</summary>
public sealed class DeadLetterService : IDeadLetterStore
{
    private readonly string _connectionString;
    private readonly IClock _clock;

    public DeadLetterService(IConfiguration configuration, IClock clock)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _clock = clock;
    }

    public async Task<IReadOnlyList<DeadOutboxMessageDto>> ListAsync(
        Guid tenantId,
        DateTimeOffset? cursorDeadAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        }
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }
        if (cursorDeadAt.HasValue != cursorId.HasValue)
        {
            throw new ArgumentException("Both cursor values must be supplied together.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT id, original_event_id, tenant_id, type, payload::text, aggregate_type, aggregate_id,
                   aggregate_version, attempts, last_error, last_error_category, dead_at,
                   replayed_at, replayed_by
            FROM platform.outbox_dead
            WHERE tenant_id = @tenantId
              AND (@cursorDeadAt IS NULL OR (dead_at, id) < (@cursorDeadAt, @cursorId))
            ORDER BY dead_at DESC, id DESC
            LIMIT @limit;";
        command.Parameters.AddWithValue("tenantId", tenantId);
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
        if (tenantId == Guid.Empty || deadMessageId == Guid.Empty)
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT id, original_event_id, tenant_id, type, payload::text, aggregate_type, aggregate_id,
                   aggregate_version, attempts, last_error, last_error_category, dead_at,
                   replayed_at, replayed_by
            FROM platform.outbox_dead
            WHERE id = @id AND tenant_id = @tenantId;";
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

    public async Task<ReplayDeadOutboxResponse?> ReplayAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty || deadMessageId == Guid.Empty)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("Replay reason must contain 1 to 500 characters.", nameof(reason));
        }

        var now = _clock.UtcNow;
        var newEventId = Guid.CreateVersion7();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            WITH replayed AS (
                UPDATE platform.outbox_dead
                SET replayed_at = @now, replayed_by = @actorId, replay_reason = @reason
                WHERE id = @deadId AND tenant_id = @tenantId AND replayed_at IS NULL
                RETURNING tenant_id, type, payload, aggregate_type, aggregate_id, aggregate_version
            )
            INSERT INTO platform.outbox
                (id, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload,
                 occurred_at, attempts, next_attempt_at, locked_by, locked_until, dispatched_at, dead_at)
            SELECT @newEventId, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload,
                   @now, 0, @now, NULL, NULL, NULL, NULL
            FROM replayed
            RETURNING id;";
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("actorId", actorId);
        command.Parameters.AddWithValue("reason", reason.Trim());
        command.Parameters.AddWithValue("deadId", deadMessageId);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("newEventId", newEventId);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReplayDeadOutboxResponse(deadMessageId, newEventId, now, actorId);
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
        reader.GetFieldValue<DateTimeOffset>(11),
        reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
        reader.IsDBNull(13) ? null : reader.GetGuid(13));
}
