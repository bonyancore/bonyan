namespace Construction.Procurement.Contracts.Events;

public sealed record PurchaseOrderModifiedV1(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    int Revision,
    Guid SupplierId,
    Guid ProjectId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset ModifiedAt) : IProcurementEvent
{
    string IProcurementEvent.EventType => "procurement.purchase-order-modified.v1";
    DateTimeOffset IProcurementEvent.OccurredAt => ModifiedAt;
}
