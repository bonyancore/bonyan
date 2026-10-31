namespace Construction.Messaging;

/// <summary>
/// Abstraction for initiating transactional execution of idempotent consumers
/// and recording processed events in platform.processed_events.
/// </summary>
public interface IProcessedEventStore
{
    /// <summary>
    /// Begins a new transaction for consumer execution.
    /// </summary>
    Task<IConsumerTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
