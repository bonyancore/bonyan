using System.Data.Common;
using Construction.Platform.Core.Application.Audit;
using Npgsql;

namespace Construction.Platform.Infrastructure.Audit;

/// <summary>Writes to the append-only <c>platform.audit_log</c> on the caller's transaction.</summary>
public sealed class PostgresAuditLog : IAuditLog
{
    public async Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        AuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(entry);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO platform.audit_log
                (id, tenant_id, at, user_id, entity_type, entity_id, action,
                 before, after, reason, ip_address, correlation_id)
            VALUES
                (@id, @tenantId, now(), @userId, @entityType, @entityId, @action,
                 @before::jsonb, @after::jsonb, @reason, @ip, @correlationId);
            """;

        command.Parameters.Add(new NpgsqlParameter("id", Guid.CreateVersion7()));
        command.Parameters.Add(new NpgsqlParameter("tenantId", entry.TenantId));
        command.Parameters.Add(new NpgsqlParameter("userId", (object?)entry.UserId ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("entityType", entry.EntityType));
        command.Parameters.Add(new NpgsqlParameter("entityId", (object?)entry.EntityId ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("action", entry.Action));
        command.Parameters.Add(new NpgsqlParameter("before", (object?)entry.Before ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("after", (object?)entry.After ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("reason", (object?)entry.Reason ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter("ip", (object?)entry.IpAddress ?? DBNull.Value));
        command.Parameters.Add(new NpgsqlParameter(
            "correlationId", (object?)entry.CorrelationId ?? DBNull.Value));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
