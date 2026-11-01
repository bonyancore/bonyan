namespace Construction.Procurement.Contracts.Events;

public sealed record PurchaseOrderCancelledV1(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    int Revision,
    string Reason,
    DateTimeOffset CancelledAt) : IProcurementEvent
{
    string IProcurementEvent.EventType => "procurement.purchase-order-cancelled.v1";
    DateTimeOffset IProcurementEvent.OccurredAt => CancelledAt;
}
