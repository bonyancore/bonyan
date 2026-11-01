namespace Construction.Procurement.Core.Domain.PurchaseOrders;

public enum PurchaseOrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Issued,
    Cancelled,
}
