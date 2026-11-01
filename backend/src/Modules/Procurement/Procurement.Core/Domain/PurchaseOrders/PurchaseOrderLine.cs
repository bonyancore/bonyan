using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.PurchaseOrders;

public sealed class PurchaseOrderLine : ITenantScopedEntity, IAuditableEntity
{
    private decimal _quantityAmount;
    private string _quantityUnit = string.Empty;

    private PurchaseOrderLine() { }

    public PurchaseOrderLine(Guid purchaseRequestItemId, string description, Quantity quantity, Money unitPrice)
    {
        if (purchaseRequestItemId == Guid.Empty)
            throw new DomainRuleViolation("procurement.po_request_item_required");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
            throw new DomainRuleViolation("procurement.po_line_description_required");
        if (quantity.Amount <= 0 || string.IsNullOrWhiteSpace(quantity.Unit))
            throw new DomainRuleViolation("procurement.po_line_quantity_invalid");
        if (unitPrice.Amount < 0 || string.IsNullOrWhiteSpace(unitPrice.Currency))
            throw new DomainRuleViolation("procurement.po_line_price_invalid");

        Id = Ids.New();
        PurchaseRequestItemId = purchaseRequestItemId;
        Description = description.Trim();
        _quantityAmount = quantity.Amount;
        _quantityUnit = quantity.Unit;
        UnitPriceAmount = unitPrice.Amount;
        Currency = unitPrice.Currency;
    }

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid PurchaseRequestItemId { get; private set; }
    public TenantId TenantId { get; set; }
    public string Description { get; private set; } = string.Empty;
    public Quantity Quantity => new(_quantityAmount, _quantityUnit);
    public decimal QuantityAmount { get => _quantityAmount; private set => _quantityAmount = value; }
    public string QuantityUnit { get => _quantityUnit; private set => _quantityUnit = value; }
    public decimal UnitPriceAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Money UnitPrice => new(UnitPriceAmount, Currency);
    public Money LineTotal => UnitPrice.Multiply(Quantity.Amount);
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void Attach(Guid orderId, TenantId tenantId)
    {
        if (orderId == Guid.Empty || !tenantId.IsValid || PurchaseOrderId != Guid.Empty)
            throw new DomainRuleViolation("procurement.po_line_attach_invalid");
        PurchaseOrderId = orderId;
        TenantId = tenantId;
    }
}
