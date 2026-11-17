namespace Construction.Messaging;

/// <summary>
/// Transactional outbox writer.
/// Publishes integration events within the caller's active database transaction.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Synchronously enqueues an event into the outbox in the current transaction.
    /// </summary>
    void Publish<TEvent>(
        TEvent evt,
        Guid? projectId = null,
        Guid? actorId = null,
        Guid? correlationId = null,
        Guid? causationId = null);

    /// <summary>
    /// Synchronously enqueues a pre-formed envelope into the outbox in the current transaction.
    /// </summary>
    void Publish<TEvent>(EventEnvelope<TEvent> envelope);

    /// <summary>
    /// Asynchronously enqueues an event into the outbox in the current transaction.
    /// </summary>
    Task PublishAsync<TEvent>(
        TEvent evt,
        Guid? projectId = null,
        Guid? actorId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously enqueues a pre-formed envelope into the outbox in the current transaction.
    /// </summary>
    Task PublishAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default);
}
