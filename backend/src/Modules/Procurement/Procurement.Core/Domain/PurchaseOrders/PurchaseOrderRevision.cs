using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.PurchaseOrders;

/// <summary>Immutable issue-time snapshot, retained when the live order is revised.</summary>
public sealed class PurchaseOrderRevision : ITenantScopedEntity, IAuditableEntity
{
    private readonly List<PurchaseOrderRevisionLine> _lines = [];
    private PurchaseOrderRevision() { }

    private PurchaseOrderRevision(Guid id, Guid purchaseOrderId, TenantId tenantId, int revision,
        Guid supplierId, Guid projectId, decimal totalAmount, string currency,
        string approvalReference, DateTimeOffset issuedAt)
    {
        Id = id;
        PurchaseOrderId = purchaseOrderId;
        TenantId = tenantId;
        Revision = revision;
        SupplierId = supplierId;
        ProjectId = projectId;
        TotalAmount = totalAmount;
        Currency = currency;
        ApprovalReference = approvalReference;
        IssuedAt = issuedAt.UtcDateTime;
    }

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public TenantId TenantId { get; set; }
    public int Revision { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid ProjectId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string ApprovalReference { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public IReadOnlyList<PurchaseOrderRevisionLine> Lines => _lines.AsReadOnly();
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal static PurchaseOrderRevision Create(PurchaseOrder order, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (issuedAt == default || string.IsNullOrWhiteSpace(order.ApprovalReference))
            throw new DomainRuleViolation("procurement.po_revision_snapshot_invalid");
        var snapshot = new PurchaseOrderRevision(Ids.New(), order.Id, order.TenantId, order.Revision,
            order.SupplierId, order.ProjectId, order.TotalAmount, order.Currency,
            order.ApprovalReference, issuedAt);
        foreach (var line in order.Lines)
            snapshot._lines.Add(PurchaseOrderRevisionLine.CopyFrom(Ids.New(), snapshot.Id, order.TenantId, line));
        return snapshot;
    }
}

public sealed class PurchaseOrderRevisionLine : ITenantScopedEntity, IAuditableEntity
{
    private PurchaseOrderRevisionLine() { }

    private PurchaseOrderRevisionLine(Guid id, Guid revisionId, TenantId tenantId,
        Guid purchaseRequestItemId, string description, decimal quantity, string unit,
        decimal unitPrice, string currency)
    {
        Id = id;
        PurchaseOrderRevisionId = revisionId;
        TenantId = tenantId;
        PurchaseRequestItemId = purchaseRequestItemId;
        Description = description;
        QuantityAmount = quantity;
        QuantityUnit = unit;
        UnitPriceAmount = unitPrice;
        Currency = currency;
    }

    public Guid Id { get; private set; }
    public Guid PurchaseOrderRevisionId { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid PurchaseRequestItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal QuantityAmount { get; private set; }
    public string QuantityUnit { get; private set; } = string.Empty;
    public decimal UnitPriceAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal static PurchaseOrderRevisionLine CopyFrom(Guid id, Guid revisionId, TenantId tenantId, PurchaseOrderLine line) =>
        new(id, revisionId, tenantId, line.PurchaseRequestItemId, line.Description,
            line.Quantity.Amount, line.Quantity.Unit, line.UnitPriceAmount, line.Currency);
}
