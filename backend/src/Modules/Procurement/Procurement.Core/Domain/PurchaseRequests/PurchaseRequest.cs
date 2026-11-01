using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.PurchaseRequests;

public sealed class PurchaseRequest : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<PurchaseRequestItem> _items = [];

    private PurchaseRequest() { }

    private PurchaseRequest(Guid id, Guid projectId, Guid requestedBy, TenantId tenantId, DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        RequestedBy = requestedBy;
        TenantId = tenantId;
        CreatedAt = createdAt.UtcDateTime;
        UpdatedAt = CreatedAt;
        Status = PurchaseRequestStatus.Draft;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid RequestedBy { get; private set; }

    public TenantId TenantId { get; set; }

    public string? RequestNumber { get; private set; }

    public string? ApprovalReference { get; private set; }

    public string? RejectionReason { get; private set; }

    public Guid? WorkPackageId { get; private set; }

    public string? WorkPackageCode { get; private set; }

    public string? WorkPackageName { get; private set; }

    public Guid? SourceEventId { get; private set; }

    public PurchaseRequestStatus Status { get; private set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public uint RowVersion { get; set; }

    public IReadOnlyList<PurchaseRequestItem> Items => _items.AsReadOnly();

    public static PurchaseRequest Create(
        Guid id,
        Guid projectId,
        Guid requestedBy,
        TenantId tenantId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainRuleViolation("procurement.purchase_request_id_required");

        if (projectId == Guid.Empty)
            throw new DomainRuleViolation("procurement.purchase_request_project_required");

        if (requestedBy == Guid.Empty)
            throw new DomainRuleViolation("procurement.purchase_request_requester_required");

        if (!tenantId.IsValid)
            throw new DomainRuleViolation("procurement.purchase_request_tenant_required");

        if (createdAt == default)
            throw new DomainRuleViolation("procurement.purchase_request_created_at_required");

        return new PurchaseRequest(id, projectId, requestedBy, tenantId, createdAt);
    }

    public void AddItem(PurchaseRequestItem item)
    {
        if (item is null)
            throw new DomainRuleViolation("procurement.purchase_request_item_required");

        EnsureDraft();
        item.AttachToRequest(Id, TenantId);
        _items.Add(item);
    }

    public void AssignRequestNumber(string requestNumber)
    {
        EnsureStatus(PurchaseRequestStatus.Draft);

        if (string.IsNullOrWhiteSpace(requestNumber) || requestNumber.Trim().Length > 80)
            throw new DomainRuleViolation("procurement.purchase_request_number_required");

        if (RequestNumber is not null)
            throw new DomainRuleViolation("procurement.purchase_request_number_already_assigned");

        RequestNumber = requestNumber.Trim();
    }

    public void AttachWorkPackageSnapshot(Guid workPackageId, string code, string name)
    {
        EnsureStatus(PurchaseRequestStatus.Draft);

        if (workPackageId == Guid.Empty)
            throw new DomainRuleViolation("procurement.work_package_id_required");

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new DomainRuleViolation("procurement.work_package_snapshot_required");

        WorkPackageId = workPackageId;
        WorkPackageCode = code.Trim();
        WorkPackageName = name.Trim();
    }

    public void SetSourceEvent(Guid eventId)
    {
        EnsureStatus(PurchaseRequestStatus.Draft);
        if (eventId == Guid.Empty)
            throw new DomainRuleViolation("procurement.source_event_id_required");
        if (SourceEventId is not null)
            throw new DomainRuleViolation("procurement.source_event_already_assigned");
        SourceEventId = eventId;
    }

    public void Submit()
    {
        EnsureStatus(PurchaseRequestStatus.Draft);

        if (_items.Count == 0)
            throw new DomainRuleViolation("procurement.purchase_request_items_required");

        Status = PurchaseRequestStatus.Submitted;
    }

    public void Approve(string? approvalReference = null)
    {
        EnsureStatus(PurchaseRequestStatus.Submitted);
        ApprovalReference = string.IsNullOrWhiteSpace(approvalReference) ? null : approvalReference.Trim();
        Status = PurchaseRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        EnsureStatus(PurchaseRequestStatus.Submitted);
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Status = PurchaseRequestStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Submitted))
            throw new DomainRuleViolation("procurement.purchase_request_invalid_transition");

        Status = PurchaseRequestStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        EnsureStatus(PurchaseRequestStatus.Draft);
    }

    private void EnsureStatus(PurchaseRequestStatus expectedStatus)
    {
        if (Status != expectedStatus)
            throw new DomainRuleViolation("procurement.purchase_request_invalid_transition");
    }
}
