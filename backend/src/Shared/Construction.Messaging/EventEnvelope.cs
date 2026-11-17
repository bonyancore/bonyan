using Construction.Kernel;

namespace Construction.Messaging;

/// <summary>
/// Non-generic envelope metadata interface.
/// </summary>
public interface IEventEnvelope
{
    Guid EventId { get; }
    string Type { get; }
    DateTimeOffset OccurredAt { get; }
    Guid TenantId { get; }
    Guid? ProjectId { get; }
    Guid? ActorId { get; }
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
    object? RawPayload { get; }
}

/// <summary>
/// Standard integration event envelope matching Spec 00 §3.9 and Brief §2.5.
/// All integration events published and consumed across modules must be wrapped in this envelope.
/// </summary>
/// <typeparam name="TPayload">The strongly-typed payload.</typeparam>
public sealed record EventEnvelope<TPayload>(
    Guid EventId,
    string Type,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid? ProjectId,
    Guid? ActorId,
    Guid CorrelationId,
    Guid? CausationId,
    TPayload Payload
) : IEventEnvelope
{
    object? IEventEnvelope.RawPayload => Payload;

    /// <summary>
    /// Creates a new envelope with a time-ordered UUID v7 EventId and UtcNow timestamp.
    /// </summary>
    public static EventEnvelope<TPayload> Create(
        TPayload payload,
        Guid tenantId,
        string? type = null,
        Guid? projectId = null,
        Guid? actorId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId must be a valid, non-empty UUID.", nameof(tenantId));
        }

        var resolvedType = !string.IsNullOrWhiteSpace(type) 
            ? type 
            : EventTypeAttribute.GetEventType<TPayload>();

        var eventId = Ids.New();
        var timestamp = occurredAt ?? DateTimeOffset.UtcNow;
        var corrId = correlationId ?? eventId;

        return new EventEnvelope<TPayload>(
            EventId: eventId,
            Type: resolvedType,
            OccurredAt: timestamp,
            TenantId: tenantId,
            ProjectId: projectId,
            ActorId: actorId,
            CorrelationId: corrId,
            CausationId: causationId,
            Payload: payload
        );
    }
}
