using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.RequestsForQuotation;

namespace Construction.Procurement.Core.Domain.PurchaseOrders;

public sealed class PurchaseOrder : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<PurchaseOrderLine> _lines = [];
    private readonly List<PurchaseOrderRevision> _revisions = [];

    private PurchaseOrder() { }

    private PurchaseOrder(Guid id, TenantId tenantId, Guid projectId, Guid supplierId, string number)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        SupplierId = supplierId;
        Number = number;
        Revision = 1;
        Status = PurchaseOrderStatus.Draft;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid ProjectId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid PurchaseRequestId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public string? ApprovalReference { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public decimal BudgetReservedAmount { get; private set; }
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyList<PurchaseOrderRevision> Revisions => _revisions.AsReadOnly();
    public decimal TotalAmount => _lines.Sum(line => line.LineTotal.Amount);
    public string Currency => _lines.FirstOrDefault()?.Currency ?? Money.DefaultCurrency;
    public DateTime? IssuedAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }

    public static PurchaseOrder CreateFromApprovedRequest(
        Guid id, string number, PurchaseRequest request, Guid supplierId,
        IReadOnlyCollection<PurchaseOrderLine> pricedLines)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status != PurchaseRequestStatus.Approved)
            throw new DomainRuleViolation("procurement.po_purchase_request_not_approved");
        if (request.Items.Count == 0)
            throw new DomainRuleViolation("procurement.po_request_items_required");
        ValidateRequestQuantities(request.Items, pricedLines);
        return Create(id, number, request.TenantId, request.ProjectId, request.Id, supplierId,
            request.Items.Select(item => item.Id).ToHashSet(), pricedLines);
    }

    public static PurchaseOrder CreateFromAwardedQuote(
        Guid id, string number, PurchaseRequest request, RequestForQuotation rfq,
        SupplierQuote awardedQuote)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rfq);
        ArgumentNullException.ThrowIfNull(awardedQuote);
        if (rfq.Status != RequestForQuotationStatus.Awarded || rfq.AwardedSupplierId != awardedQuote.SupplierId ||
            rfq.PurchaseRequestId != request.Id || rfq.TenantId != request.TenantId)
            throw new DomainRuleViolation("procurement.po_quote_not_awarded_for_request");

        var lines = awardedQuote.Lines.Select(line => new PurchaseOrderLine(
            line.PurchaseRequestItemId, line.Description, line.Quantity, line.UnitPrice)).ToArray();
        ValidateRequestQuantities(request.Items, lines);
        return Create(id, number, request.TenantId, request.ProjectId, request.Id,
            awardedQuote.SupplierId, request.Items.Select(item => item.Id).ToHashSet(), lines);
    }

    public void ReplaceLines(IReadOnlyCollection<PurchaseOrderLine> revisedLines)
    {
        if (Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.PendingApproval)
            throw new DomainRuleViolation("procurement.po_cannot_be_modified");
        ValidateLines(revisedLines, _lines.Select(line => line.PurchaseRequestItemId).ToHashSet());
        if (_revisions.Any(item => item.Revision == Revision))
            Revision++;
        _lines.Clear();
        foreach (var line in revisedLines)
        {
            line.Attach(Id, TenantId);
            _lines.Add(line);
        }
        Status = PurchaseOrderStatus.Draft;
        ApprovalReference = null;
        IssuedAt = null;
    }

    public void SubmitForApproval(ProjectBudget projectBudget)
    {
        EnsureStatus(PurchaseOrderStatus.Draft);
        ArgumentNullException.ThrowIfNull(projectBudget);
        if (_lines.Count == 0)
            throw new DomainRuleViolation("procurement.po_lines_required");
        if (projectBudget.ProjectId != ProjectId || projectBudget.TenantId != TenantId ||
            Currency != projectBudget.Currency || BudgetReservedAmount != 0)
            throw new DomainRuleViolation("procurement.project_budget_mismatch");
        projectBudget.Reserve(new Money(TotalAmount, Currency));
        BudgetReservedAmount = TotalAmount;
        Status = PurchaseOrderStatus.PendingApproval;
    }

    public void ReleaseBudgetReservation(ProjectBudget projectBudget)
    {
        ArgumentNullException.ThrowIfNull(projectBudget);
        if (BudgetReservedAmount == 0)
            return;
        if (projectBudget.ProjectId != ProjectId || projectBudget.TenantId != TenantId)
            throw new DomainRuleViolation("procurement.project_budget_mismatch");
        projectBudget.Release(new Money(BudgetReservedAmount, Currency));
        BudgetReservedAmount = 0;
    }

    public void Approve(string reference)
    {
        EnsureStatus(PurchaseOrderStatus.PendingApproval);
        if (string.IsNullOrWhiteSpace(reference) || reference.Trim().Length > 200)
            throw new DomainRuleViolation("procurement.po_approval_reference_required");
        ApprovalReference = reference.Trim();
        Status = PurchaseOrderStatus.Approved;
    }

    public void Reject(string reason)
    {
        EnsureStatus(PurchaseOrderStatus.PendingApproval);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new DomainRuleViolation("procurement.po_rejection_reason_required");
        RejectionReason = reason.Trim();
        Status = PurchaseOrderStatus.Rejected;
    }

    public PurchaseOrderIssuedV1 Issue(DateTimeOffset issuedAt)
    {
        EnsureStatus(PurchaseOrderStatus.Approved);
        if (issuedAt == default)
            throw new DomainRuleViolation("procurement.po_issue_time_required");
        Status = PurchaseOrderStatus.Issued;
        IssuedAt = issuedAt.UtcDateTime;
        if (_revisions.Any(item => item.Revision == Revision))
            throw new DomainRuleViolation("procurement.po_revision_already_issued");
        _revisions.Add(PurchaseOrderRevision.Create(this, issuedAt));
        return new PurchaseOrderIssuedV1(Ids.New(), TenantId.Value, Id, Number, Revision,
            SupplierId, ProjectId, TotalAmount, Currency, issuedAt.ToUniversalTime());
    }

    public PurchaseOrderModifiedV1 GetModifiedEvent(DateTimeOffset modifiedAt)
    {
        if (Status != PurchaseOrderStatus.Issued || Revision < 2)
            throw new DomainRuleViolation("procurement.po_revision_not_issued");
        return new PurchaseOrderModifiedV1(Ids.New(), TenantId.Value, Id, Number, Revision,
            SupplierId, ProjectId, TotalAmount, Currency, modifiedAt.ToUniversalTime());
    }

    public PurchaseOrderCancelledV1 Cancel(string reason, DateTimeOffset cancelledAt)
    {
        if (Status is not (PurchaseOrderStatus.Issued or PurchaseOrderStatus.Approved))
            throw new DomainRuleViolation("procurement.po_invalid_transition");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleViolation("procurement.po_cancellation_reason_required");
        CancellationReason = reason.Trim();
        Status = PurchaseOrderStatus.Cancelled;
        return new PurchaseOrderCancelledV1(Ids.New(), TenantId.Value, Id, Number, Revision,
            CancellationReason, cancelledAt.ToUniversalTime());
    }

    private static PurchaseOrder Create(Guid id, string number, TenantId tenantId, Guid projectId,
        Guid requestId, Guid supplierId, IReadOnlySet<Guid> expectedItemIds,
        IReadOnlyCollection<PurchaseOrderLine> lines)
    {
        if (id == Guid.Empty || projectId == Guid.Empty || requestId == Guid.Empty || supplierId == Guid.Empty)
            throw new DomainRuleViolation("procurement.po_identity_required");
        if (!tenantId.IsValid)
            throw new DomainRuleViolation("procurement.po_tenant_required");
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 80)
            throw new DomainRuleViolation("procurement.po_number_required");
        ValidateLines(lines, expectedItemIds);
        var order = new PurchaseOrder(id, tenantId, projectId, supplierId, number.Trim())
        {
            PurchaseRequestId = requestId,
        };
        foreach (var line in lines)
        {
            line.Attach(id, tenantId);
            order._lines.Add(line);
        }
        return order;
    }

    private static void ValidateLines(IReadOnlyCollection<PurchaseOrderLine> lines, IReadOnlySet<Guid> expectedItemIds)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0 || lines.Select(line => line.PurchaseRequestItemId).Distinct().Count() != lines.Count ||
            !expectedItemIds.SetEquals(lines.Select(line => line.PurchaseRequestItemId)))
            throw new DomainRuleViolation("procurement.po_lines_must_match_request");
        if (lines.Select(line => line.Currency).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new DomainRuleViolation("procurement.po_currency_mismatch");
    }

    private static void ValidateRequestQuantities(
        IReadOnlyCollection<PurchaseRequestItem> requestItems,
        IReadOnlyCollection<PurchaseOrderLine> orderLines)
    {
        ArgumentNullException.ThrowIfNull(orderLines);
        if (requestItems.Count != orderLines.Count)
            throw new DomainRuleViolation("procurement.po_lines_must_match_request");
        foreach (var line in orderLines)
        {
            var requestItem = requestItems.SingleOrDefault(item => item.Id == line.PurchaseRequestItemId)
                ?? throw new DomainRuleViolation("procurement.po_lines_must_match_request");
            if (requestItem.Quantity != line.Quantity)
                throw new DomainRuleViolation("procurement.po_quantity_must_match_request");
        }
    }

    private void EnsureStatus(PurchaseOrderStatus expected)
    {
        if (Status != expected)
            throw new DomainRuleViolation("procurement.po_invalid_transition");
    }
}
