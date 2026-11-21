using System.Data.Common;

namespace Construction.Platform.Core.Application.Audit;

/// <summary>
/// One append-only audit row (spec 01 §7.5 / §15.3). <c>before</c>/<c>after</c> are small JSON
/// snapshots; hash chaining and the search API stay in PLT-109.
/// </summary>
public sealed record AuditEntry(
    Guid TenantId,
    Guid? UserId,
    string EntityType,
    Guid? EntityId,
    string Action,
    string? Before = null,
    string? After = null,
    string? Reason = null,
    string? IpAddress = null,
    Guid? CorrelationId = null);

/// <summary>
/// Writes an audit row on the caller's connection and transaction, so an audited change and its
/// audit trail commit or roll back together.
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        AuditEntry entry,
        CancellationToken cancellationToken = default);
}
