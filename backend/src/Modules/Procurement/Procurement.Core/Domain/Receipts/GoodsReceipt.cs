using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseOrders;

namespace Construction.Procurement.Core.Domain.Receipts;

public sealed class GoodsReceipt : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<GoodsReceiptLine> _lines = [];
    private GoodsReceipt() { }

    private GoodsReceipt(Guid id, TenantId tenantId, Guid purchaseOrderId, DateTimeOffset receivedAt)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        ReceivedAt = receivedAt.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid PurchaseOrderId { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime? ReversedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public bool IsReversed => ReversedAt.HasValue;
    public IReadOnlyList<GoodsReceiptLine> Lines => _lines.AsReadOnly();
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }

    public static GoodsReceipt Record(
        Guid id,
        PurchaseOrder order,
        IReadOnlyCollection<GoodsReceiptLine> lines,
        IReadOnlyDictionary<Guid, Quantity> previouslyReceived,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(previouslyReceived);
        if (order.Status != PurchaseOrderStatus.Issued)
            throw new DomainRuleViolation("procurement.receipt_order_not_issued");
        if (id == Guid.Empty || receivedAt == default)
            throw new DomainRuleViolation("procurement.receipt_identity_required");
        if (lines.Count == 0 || lines.Select(line => line.PurchaseOrderLineId).Distinct().Count() != lines.Count)
            throw new DomainRuleViolation("procurement.receipt_lines_invalid");

        foreach (var receiptLine in lines)
        {
            var orderLine = order.Lines.SingleOrDefault(line => line.Id == receiptLine.PurchaseOrderLineId)
                ?? throw new DomainRuleViolation("procurement.receipt_order_line_not_found");
            var previous = previouslyReceived.TryGetValue(orderLine.Id, out var quantity)
                ? quantity : Quantity.Zero(orderLine.Quantity.Unit);
            var receivedTotal = previous + receiptLine.Quantity;
            if (receivedTotal.Unit != orderLine.Quantity.Unit || receivedTotal.Amount > orderLine.Quantity.Amount)
                throw new DomainRuleViolation("procurement.receipt_quantity_exceeds_order");
            receiptLine.Attach(id, order.TenantId, orderLine.Id);
        }

        var receipt = new GoodsReceipt(id, order.TenantId, order.Id, receivedAt);
        receipt._lines.AddRange(lines);
        return receipt;
    }

    public void Reverse(string reason, DateTimeOffset reversedAt)
    {
        if (IsReversed)
            throw new DomainRuleViolation("procurement.receipt_already_reversed");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || reversedAt == default)
            throw new DomainRuleViolation("procurement.receipt_reversal_details_required");
        ReversalReason = reason.Trim();
        ReversedAt = reversedAt.UtcDateTime;
    }
}

public sealed class GoodsReceiptLine : ITenantScopedEntity, IAuditableEntity
{
    private decimal _quantityAmount;
    private string _quantityUnit = string.Empty;
    private GoodsReceiptLine() { }

    public GoodsReceiptLine(Guid purchaseOrderLineId, Quantity quantity)
    {
        if (purchaseOrderLineId == Guid.Empty || quantity.Amount <= 0 || string.IsNullOrWhiteSpace(quantity.Unit))
            throw new DomainRuleViolation("procurement.receipt_line_invalid");
        Id = Ids.New();
        PurchaseOrderLineId = purchaseOrderLineId;
        _quantityAmount = quantity.Amount;
        _quantityUnit = quantity.Unit;
    }

    public Guid Id { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public TenantId TenantId { get; set; }
    public Quantity Quantity => new(_quantityAmount, _quantityUnit);
    public decimal QuantityAmount { get => _quantityAmount; private set => _quantityAmount = value; }
    public string QuantityUnit { get => _quantityUnit; private set => _quantityUnit = value; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void Attach(Guid receiptId, TenantId tenantId, Guid orderLineId)
    {
        if (GoodsReceiptId != Guid.Empty || receiptId == Guid.Empty || orderLineId != PurchaseOrderLineId)
            throw new DomainRuleViolation("procurement.receipt_line_attach_invalid");
        GoodsReceiptId = receiptId;
        TenantId = tenantId;
    }
}
