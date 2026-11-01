using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.PurchaseRequests;

public sealed class PurchaseRequestItem : ITenantScopedEntity, IAuditableEntity
{
    private decimal _quantityAmount;
    private string _quantityUnit = string.Empty;

    private PurchaseRequestItem() { }

    public PurchaseRequestItem(string description, Quantity quantity)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainRuleViolation("procurement.purchase_request_item_description_required");

        if (string.IsNullOrWhiteSpace(quantity.Unit))
            throw new DomainRuleViolation("procurement.purchase_request_item_unit_required");

        if (quantity.Amount <= 0)
            throw new DomainRuleViolation("procurement.purchase_request_item_quantity_must_be_positive");

        Id = Ids.New();
        Description = description.Trim();
        _quantityAmount = quantity.Amount;
        _quantityUnit = quantity.Unit;
    }

    public Guid Id { get; private set; }

    public Guid PurchaseRequestId { get; private set; }

    public TenantId TenantId { get; set; }

    public string Description { get; private set; } = string.Empty;

    public Quantity Quantity => new(_quantityAmount, _quantityUnit);

    public decimal QuantityAmount
    {
        get => _quantityAmount;
        private set => _quantityAmount = value;
    }

    public string QuantityUnit
    {
        get => _quantityUnit;
        private set => _quantityUnit = value;
    }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal void AttachToRequest(Guid purchaseRequestId, TenantId tenantId)
    {
        if (purchaseRequestId == Guid.Empty)
            throw new DomainRuleViolation("procurement.purchase_request_id_required");

        if (!tenantId.IsValid)
            throw new DomainRuleViolation("procurement.purchase_request_tenant_required");

        if (PurchaseRequestId != Guid.Empty)
            throw new DomainRuleViolation("procurement.purchase_request_item_already_attached");

        PurchaseRequestId = purchaseRequestId;
        TenantId = tenantId;
    }
}
