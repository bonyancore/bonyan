namespace Construction.Procurement.Contracts.Events;

public sealed record SupplierPaymentScheduledV1(
    Guid EventId,
    Guid TenantId,
    Guid InvoiceId,
    Guid PurchaseOrderId,
    Guid SupplierId,
    string PaymentReference,
    decimal Amount,
    string Currency,
    DateOnly ScheduledFor,
    DateTimeOffset ScheduledAt) : IProcurementEvent
{
    string IProcurementEvent.EventType => "procurement.supplier-payment-scheduled.v1";
    DateTimeOffset IProcurementEvent.OccurredAt => ScheduledAt;
}
