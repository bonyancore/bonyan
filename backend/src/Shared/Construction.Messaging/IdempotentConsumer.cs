using Construction.Kernel.Tenancy;

namespace Construction.Messaging;

/// <summary>
/// Base class for idempotent integration event consumers.
/// Enforces tenant context isolation (S1/S2) and duplicate suppression via platform.processed_events.
/// </summary>
/// <typeparam name="TEvent">The event payload type.</typeparam>
public abstract class IdempotentConsumer<TEvent> : IEventHandler<TEvent>
{
    private readonly IProcessedEventStore _eventStore;
    private readonly ITenantSetter _tenantSetter;

    protected IdempotentConsumer(IProcessedEventStore eventStore, ITenantSetter tenantSetter)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _tenantSetter = tenantSetter ?? throw new ArgumentNullException(nameof(tenantSetter));
    }

    /// <summary>
    /// Unique consumer identifier, defaults to the fully-qualified class name.
    /// </summary>
    public virtual string ConsumerName => GetType().FullName ?? GetType().Name;

    /// <summary>
    /// Outer template method: validates tenant, sets tenant context, opens transaction,
    /// attempts duplicate suppression insert, runs business logic, and commits.
    /// Traps: Does NOT catch handler exceptions inside the base class (allowing rollback).
    /// </summary>
    public async Task HandleAsync(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Security check S1/S2: envelope must carry a valid tenantId
        if (envelope.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Event '{envelope.EventId}' of type '{envelope.Type}' rejected: missing or empty tenantId.");
        }

        // Set the ambient tenant context from the envelope for this execution scope
        using var tenantScope = _tenantSetter.BeginScope(new TenantId(envelope.TenantId));

        // Open transaction for processing
        await using var txContext = await _eventStore.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // INSERT INTO platform.processed_events(consumer, event_id) ON CONFLICT DO NOTHING
        bool isNew = await txContext.TryRecordProcessedAsync(ConsumerName, envelope.EventId, cancellationToken).ConfigureAwait(false);
        if (!isNew)
        {
            // 0 rows inserted => duplicate event; skip execution
            return;
        }

        // Run HandleAsync inside the same transaction
        // Do NOT catch handler exceptions inside the base class
        await HandleInTransactionAsync(envelope, txContext, cancellationToken).ConfigureAwait(false);

        // Commit transaction
        await txContext.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the business logic within the consumer transaction.
    /// Any exception thrown here will cause the transaction to roll back without persisting the processed_events row.
    /// </summary>
    protected abstract Task HandleInTransactionAsync(
        EventEnvelope<TEvent> envelope,
        IConsumerTransactionContext context,
        CancellationToken cancellationToken);
}
