namespace Construction.Procurement.Contracts.Events;

public interface IProcurementEvent
{
    Guid EventId { get; }
    Guid TenantId { get; }
    string EventType { get; }
    DateTimeOffset OccurredAt { get; }
}
