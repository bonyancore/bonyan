using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Core.Core.Domain.Projects;

/// <summary>
/// Project aggregate (Spec 02 §4.2.1: core.projects).
/// Lifecycle follows §5.1; business rules BR-COR-10, BR-COR-11.
/// Guards that need other modules' data (readiness, usage, close providers)
/// are NOT evaluated here; the application layer does that before calling the actions.
/// </summary>
public sealed class Project : ITenantScopedEntity, IAuditableEntity
{
    private Project() { } // For EF Core

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public ProjectType ProjectType { get; private set; }
    public ProjectStatus Status { get; private set; }
    public string ContractCurrency { get; private set; } = default!;
    public Guid? ClientPartyId { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public ProjectOriginType OriginType { get; private set; }
    public Guid? OriginId { get; private set; }
    public string? HoldReason { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Manual creation: the project starts in Setup (Spec 02 §5.1).
    /// The code comes from the PRJ number series and is assigned by the caller (BR-COR-10).
    /// Client and planned dates are required later, at StartSetup/Activate (BR-COR-11).
    /// </summary>
    public static Project Create(
        Guid id,
        TenantId tenantId,
        string code,
        string nameAr,
        ProjectType projectType,
        string contractCurrency,
        string? nameEn = null,
        Guid? clientPartyId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null) =>
        Build(id, tenantId, code, nameAr, projectType, contractCurrency, nameEn,
            clientPartyId, plannedStartDate, plannedEndDate,
            ProjectStatus.Setup, ProjectOriginType.Manual, originId: null);

    /// <summary>
    /// Draft created from a won tender (Spec 02 §5.1, BR-COR-13). May be incomplete.
    /// </summary>
    public static Project CreateDraft(
        Guid id,
        TenantId tenantId,
        string code,
        string nameAr,
        ProjectType projectType,
        string contractCurrency,
        Guid tenderId,
        string? nameEn = null,
        Guid? clientPartyId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null)
    {
        if (tenderId == Guid.Empty)
            throw new ArgumentException("Tender id cannot be empty.", nameof(tenderId));

        return Build(id, tenantId, code, nameAr, projectType, contractCurrency, nameEn,
            clientPartyId, plannedStartDate, plannedEndDate,
            ProjectStatus.Draft, ProjectOriginType.Tender, tenderId);
    }

    /// <summary>draft -> setup (start-setup). Needs a client and planned dates (BR-COR-11).</summary>
    public void StartSetup()
    {
        EnsureTransition(ProjectStatus.Setup, ProjectStatus.Draft);
        EnsureClientAndDatesPresent();
        Status = ProjectStatus.Setup;
    }

    /// <summary>
    /// setup -> active (activate). The actual start date is a date in the site timezone,
    /// computed by the caller. Readiness checks are evaluated by the application layer.
    /// </summary>
    public void Activate(DateOnly actualStartDate, DateTimeOffset now)
    {
        EnsureTransition(ProjectStatus.Active, ProjectStatus.Setup);
        EnsureClientAndDatesPresent();
        Status = ProjectStatus.Active;
        ActualStartDate = actualStartDate;
        ActivatedAt = now;
    }

    /// <summary>active -> on_hold (hold). A reason is required.</summary>
    public void Hold(string reason)
    {
        EnsureTransition(ProjectStatus.OnHold, ProjectStatus.Active);

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleViolation("core.project_reason_required");

        Status = ProjectStatus.OnHold;
        HoldReason = reason.Trim();
    }

    /// <summary>on_hold -> active (resume).</summary>
    public void Resume()
    {
        EnsureTransition(ProjectStatus.Active, ProjectStatus.OnHold);
        Status = ProjectStatus.Active;
        HoldReason = null;
    }

    /// <summary>active -> closing (begin-closing).</summary>
    public void BeginClosing()
    {
        EnsureTransition(ProjectStatus.Closing, ProjectStatus.Active);
        Status = ProjectStatus.Closing;
    }

    /// <summary>draft or setup -> cancelled (cancel). "No usage" is checked by the application layer.</summary>
    public void Cancel()
    {
        EnsureTransition(ProjectStatus.Cancelled, ProjectStatus.Draft, ProjectStatus.Setup);
        Status = ProjectStatus.Cancelled;
    }

    private static Project Build(
        Guid id,
        TenantId tenantId,
        string code,
        string nameAr,
        ProjectType projectType,
        string contractCurrency,
        string? nameEn,
        Guid? clientPartyId,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        ProjectStatus status,
        ProjectOriginType originType,
        Guid? originId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ProjectId cannot be empty.", nameof(id));

        tenantId.EnsureValid();

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Project code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(nameAr))
            throw new DomainRuleViolation("core.project_name_required");

        if (string.IsNullOrWhiteSpace(contractCurrency))
            throw new DomainRuleViolation("core.currency_unknown");

        // planned_end_date >= planned_start_date when both are present (Spec 02 §4.2.1, BR-COR-11).
        if (plannedStartDate is not null && plannedEndDate is not null && plannedEndDate < plannedStartDate)
            throw new DomainRuleViolation("core.project_dates_invalid");

        return new Project
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim(),
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            ProjectType = projectType,
            Status = status,
            ContractCurrency = contractCurrency.Trim(),
            ClientPartyId = clientPartyId,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            OriginType = originType,
            OriginId = originId
        };
    }

    // The action must start from one of the allowed statuses AND be a legal transition (Spec 02 §5.1).
    private void EnsureTransition(ProjectStatus to, params ProjectStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status) || !ProjectTransitions.CanTransition(Status, to))
            throw new DomainRuleViolation("core.project_invalid_transition");
    }

    // BR-COR-11: leaving draft / activating needs a client and planned dates.
    private void EnsureClientAndDatesPresent()
    {
        if (ClientPartyId is null)
            throw new DomainRuleViolation("core.project_client_required");

        if (PlannedStartDate is null || PlannedEndDate is null)
            throw new DomainRuleViolation("core.project_dates_invalid");
    }
}
