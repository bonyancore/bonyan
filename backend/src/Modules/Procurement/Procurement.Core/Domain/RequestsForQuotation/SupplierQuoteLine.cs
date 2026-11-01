using Construction.Kernel;

namespace Construction.Procurement.Core.Domain.RequestsForQuotation;

public sealed class SupplierQuoteLine : Construction.Kernel.Persistence.ITenantScopedEntity, Construction.Kernel.Persistence.IAuditableEntity
{
    private decimal _quantityAmount;
    private string _quantityUnit = string.Empty;
    private SupplierQuoteLine() { }

    public SupplierQuoteLine(Guid purchaseRequestItemId, string description, Quantity quantity, Money unitPrice)
    {
        if (purchaseRequestItemId == Guid.Empty)
            throw new DomainRuleViolation("procurement.quote_request_item_required");

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
            throw new DomainRuleViolation("procurement.quote_line_description_required");

        if (quantity.Amount <= 0 || string.IsNullOrWhiteSpace(quantity.Unit))
            throw new DomainRuleViolation("procurement.quote_line_quantity_invalid");

        if (string.IsNullOrWhiteSpace(unitPrice.Currency) || unitPrice.Amount < 0)
            throw new DomainRuleViolation("procurement.quote_line_price_invalid");

        PurchaseRequestItemId = purchaseRequestItemId;
        Description = description.Trim();
        _quantityAmount = quantity.Amount;
        _quantityUnit = quantity.Unit;
        UnitPriceAmount = unitPrice.Amount;
        Currency = unitPrice.Currency;
        Id = Ids.New();
    }

    public Guid Id { get; private set; }
    public Guid SupplierQuoteId { get; private set; }
    public Guid PurchaseRequestItemId { get; private set; }
    public Construction.Kernel.Tenancy.TenantId TenantId { get; set; }

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

    internal void Attach(Guid quoteId, Construction.Kernel.Tenancy.TenantId tenantId)
    {
        if (quoteId == Guid.Empty || (SupplierQuoteId != Guid.Empty && SupplierQuoteId != quoteId))
            throw new DomainRuleViolation("procurement.quote_line_attach_invalid");
        SupplierQuoteId = quoteId;
        TenantId = tenantId;
    }
}
