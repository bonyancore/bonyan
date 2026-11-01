namespace Construction.Procurement.Contracts.Events;

public sealed record PurchaseOrderIssuedV1(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    int Revision,
    Guid SupplierId,
    Guid ProjectId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset IssuedAt) : IProcurementEvent
{
    string IProcurementEvent.EventType => "procurement.purchase-order-issued.v1";
    DateTimeOffset IProcurementEvent.OccurredAt => IssuedAt;
}
