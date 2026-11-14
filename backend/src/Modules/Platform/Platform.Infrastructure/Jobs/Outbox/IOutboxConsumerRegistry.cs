using Construction.Persistence;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Registry and invoker for integration event consumers during outbox batch processing.
/// </summary>
public interface IOutboxConsumerRegistry
{
    /// <summary>
    /// Delivers an outbox message to all registered typed handlers for its event type.
    /// If no handlers are registered, logs and completes normally (at-least-once discard).
    /// If any handler throws, the exception propagates to trigger outbox retry backoff.
    /// </summary>
    Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a payload type for an event type identifier.
    /// </summary>
    void RegisterEventType(string eventType, Type payloadType);
}
