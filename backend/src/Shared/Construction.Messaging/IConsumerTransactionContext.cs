using System.Data.Common;

namespace Construction.Messaging;

/// <summary>
/// Represents the transactional context for an idempotent consumer execution.
/// </summary>
public interface IConsumerTransactionContext : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Attempts to record the event as processed for this consumer in the transaction.
    /// Returns true if the record was inserted (first time seeing this event).
    /// Returns false if already processed (duplicate, 0 rows inserted).
    /// </summary>
    Task<bool> TryRecordProcessedAsync(string consumer, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional underlying database transaction if backed by ADO.NET.
    /// </summary>
    DbTransaction? DbTransaction => null;

    /// <summary>
    /// Optional underlying database connection if backed by ADO.NET.
    /// </summary>
    DbConnection? DbConnection => null;
}
