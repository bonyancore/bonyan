using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;

namespace Construction.Procurement.Core.Domain.Outbox;

public sealed class ProcurementOutboxMessage : ITenantScopedEntity, IAuditableEntity
{
    private ProcurementOutboxMessage() { }

    private ProcurementOutboxMessage(Guid id, TenantId tenantId, string eventType, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt.UtcDateTime;
        NextAttemptAt = OccurredAt;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static ProcurementOutboxMessage Create(IProcurementEvent @event, string eventType, string payload, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event.EventId == Guid.Empty || @event.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(payload) || occurredAt == default)
            throw new ArgumentException("An outbox event must have identity, tenant, type, payload, and timestamp.");
        return new ProcurementOutboxMessage(@event.EventId, new TenantId(@event.TenantId), eventType, payload, occurredAt);
    }

    public void MarkPublished(DateTimeOffset publishedAt) => PublishedAt = publishedAt.UtcDateTime;

    public void MarkFailed(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown event delivery error" : error[..Math.Min(error.Length, 2000)];
        NextAttemptAt = nextAttemptAt.UtcDateTime;
    }
}
