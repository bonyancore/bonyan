using System.Data;
using System.Data.Common;
using System.Globalization;
using Construction.Persistence;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// PostgreSQL implementation of IOutboxRepository using ADO.NET and exact recipe queries.
/// </summary>
public sealed class PostgresOutboxRepository : IOutboxRepository
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionFactory;
    private readonly string _tableName;

    public PostgresOutboxRepository(
        Func<CancellationToken, Task<DbConnection>> connectionFactory,
        string tableName = "platform.outbox")
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _tableName = tableName;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var seconds = Math.Max(1, (int)leaseDuration.TotalSeconds);
        var query = $@"
            WITH cand AS (
              SELECT o.id FROM {_tableName} o
              WHERE o.dispatched_at IS NULL AND o.dead_at IS NULL
                AND o.next_attempt_at <= now()
                AND (o.locked_until IS NULL OR o.locked_until < now())
                AND NOT EXISTS (
                  SELECT 1 FROM {_tableName} p
                  WHERE p.aggregate_id = o.aggregate_id AND p.id < o.id
                    AND p.dispatched_at IS NULL)
              ORDER BY o.id LIMIT @batch
              FOR UPDATE SKIP LOCKED)
            UPDATE {_tableName} o 
            SET locked_by = @me, 
                locked_until = now() + (interval '1 second' * {seconds})
            FROM cand 
            WHERE o.id = cand.id 
            RETURNING o.id, o.tenant_id, o.aggregate_type, o.aggregate_id, o.aggregate_version, 
                      o.type, o.payload, o.occurred_at, o.attempts, o.next_attempt_at, 
                      o.locked_by, o.locked_until, o.dispatched_at, o.dead_at;";

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = query;

        var pBatch = cmd.CreateParameter();
        pBatch.ParameterName = "batch";
        pBatch.Value = batchSize;
        cmd.Parameters.Add(pBatch);

        var pMe = cmd.CreateParameter();
        pMe.ParameterName = "me";
        pMe.Value = workerId;
        cmd.Parameters.Add(pMe);

        var list = new List<OutboxMessage>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ReadMessage(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return list;
    }

    public async Task MarkDispatchedAsync(
        Guid messageId,
        DateTimeOffset dispatchedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            UPDATE {_tableName}
            SET dispatched_at = @dispatchedAt,
                locked_by = NULL,
                locked_until = NULL
            WHERE id = @id;";

        var pId = cmd.CreateParameter();
        pId.ParameterName = "id";
        pId.Value = messageId;
        cmd.Parameters.Add(pId);

        var pDisp = cmd.CreateParameter();
        pDisp.ParameterName = "dispatchedAt";
        pDisp.Value = dispatchedAt;
        cmd.Parameters.Add(pDisp);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        OutboxMessage message,
        int attempts,
        DateTimeOffset nextAttemptAt,
        bool isDead,
        string lastError,
        string errorCategory,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var setTenant = connection.CreateCommand())
        {
            setTenant.Transaction = transaction;
            setTenant.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
            var tenantParameter = setTenant.CreateParameter();
            tenantParameter.ParameterName = "tenantId";
            tenantParameter.Value = message.TenantId.ToString();
            setTenant.Parameters.Add(tenantParameter);
            await setTenant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $@"
            UPDATE {_tableName}
            SET attempts = @attempts,
                next_attempt_at = @nextAttemptAt,
                dead_at = @deadAt,
                locked_by = NULL,
                locked_until = NULL
            WHERE id = @id AND tenant_id = @tenantId;";

        var pId = cmd.CreateParameter();
        pId.ParameterName = "id";
        pId.Value = message.Id;
        cmd.Parameters.Add(pId);

        var pTenantId = cmd.CreateParameter();
        pTenantId.ParameterName = "tenantId";
        pTenantId.Value = message.TenantId;
        cmd.Parameters.Add(pTenantId);

        var pAtt = cmd.CreateParameter();
        pAtt.ParameterName = "attempts";
        pAtt.Value = attempts;
        cmd.Parameters.Add(pAtt);

        var pNext = cmd.CreateParameter();
        pNext.ParameterName = "nextAttemptAt";
        pNext.Value = nextAttemptAt;
        cmd.Parameters.Add(pNext);

        var pDead = cmd.CreateParameter();
        pDead.ParameterName = "deadAt";
        pDead.Value = isDead ? (object)DateTimeOffset.UtcNow : DBNull.Value;
        cmd.Parameters.Add(pDead);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (isDead)
        {
            await using var dead = connection.CreateCommand();
            dead.Transaction = transaction;
            dead.CommandText = $@"
                INSERT INTO platform.outbox_dead
                    (id, original_event_id, tenant_id, type, payload, aggregate_type, aggregate_id,
                     aggregate_version, attempts, last_error, last_error_category, dead_at)
                SELECT @deadId, id, tenant_id, type, payload, aggregate_type, aggregate_id,
                       aggregate_version, attempts, @lastError, @errorCategory, dead_at
                FROM {_tableName}
                WHERE id = @id AND tenant_id = @tenantId
                ON CONFLICT (original_event_id) DO NOTHING;";

            AddParameter(dead, "deadId", Guid.CreateVersion7());
            AddParameter(dead, "id", message.Id);
            AddParameter(dead, "tenantId", message.TenantId);
            AddParameter(dead, "lastError", lastError);
            AddParameter(dead, "errorCategory", errorCategory);
            await dead.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OutboxMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT 
                COALESCE(EXTRACT(EPOCH FROM (now() - MIN(occurred_at))), 0) AS lag_seconds,
                COUNT(*) AS pending_count,
                COALESCE(SUM(attempts), 0) AS total_attempts
            FROM {_tableName}
            WHERE dispatched_at IS NULL AND dead_at IS NULL;";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var lag = Convert.ToDouble(reader["lag_seconds"], CultureInfo.InvariantCulture);
            var pending = Convert.ToInt64(reader["pending_count"], CultureInfo.InvariantCulture);
            var attempts = Convert.ToInt64(reader["total_attempts"], CultureInfo.InvariantCulture);
            return new OutboxMetrics(Math.Max(0, lag), pending, attempts);
        }

        return new OutboxMetrics(0, 0, 0);
    }

    public async Task<int> PurgeDispatchedAsync(
        DateTimeOffset olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            DELETE FROM {_tableName}
            WHERE id IN (
                SELECT id FROM {_tableName}
                WHERE dispatched_at IS NOT NULL
                  AND dispatched_at < @olderThan
                LIMIT @batchSize
            );";

        var pOlder = cmd.CreateParameter();
        pOlder.ParameterName = "olderThan";
        pOlder.Value = olderThan;
        cmd.Parameters.Add(pOlder);

        var pBatch = cmd.CreateParameter();
        pBatch.ParameterName = "batchSize";
        pBatch.Value = batchSize;
        cmd.Parameters.Add(pBatch);

        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxMessage ReadMessage(DbDataReader reader)
    {
        return new OutboxMessage
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            TenantId = reader.GetGuid(reader.GetOrdinal("tenant_id")),
            AggregateType = reader.GetString(reader.GetOrdinal("aggregate_type")),
            AggregateId = reader.GetGuid(reader.GetOrdinal("aggregate_id")),
            AggregateVersion = reader.GetInt64(reader.GetOrdinal("aggregate_version")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Payload = reader.GetString(reader.GetOrdinal("payload")),
            OccurredAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at")),
            Attempts = reader.GetInt32(reader.GetOrdinal("attempts")),
            NextAttemptAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("next_attempt_at")),
            LockedBy = reader.IsDBNull(reader.GetOrdinal("locked_by")) ? null : reader.GetString(reader.GetOrdinal("locked_by")),
            LockedUntil = reader.IsDBNull(reader.GetOrdinal("locked_until")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("locked_until")),
            DispatchedAt = reader.IsDBNull(reader.GetOrdinal("dispatched_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("dispatched_at")),
            DeadAt = reader.IsDBNull(reader.GetOrdinal("dead_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("dead_at"))
        };
    }

    private static Npgsql.NpgsqlParameter AddParameter(DbCommand command, string name, object value)
    {
        var parameter = new Npgsql.NpgsqlParameter { ParameterName = name, Value = value };
        command.Parameters.Add(parameter);
        return parameter;
    }
}
