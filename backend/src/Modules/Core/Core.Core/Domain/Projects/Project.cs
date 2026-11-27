using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Core.Core.Domain.Projects;

/// <summary>
/// Project aggregate (Spec 02 §4.2.1: core.projects).
/// Lifecycle follows §5.1; business rules BR-COR-10 and BR-COR-11.
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
    public ProjectOriginType OriginType { get; private set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Manual creation: the project starts in Setup (Spec 02 §5.1).
    /// The code comes from the PRJ number series and is assigned by the caller (BR-COR-10).
    /// Client and planned dates are required later, when the project leaves draft/activates (BR-COR-11).
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
        DateOnly? plannedEndDate = null)
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

        EnsureDatesValid(plannedStartDate, plannedEndDate);

        return new Project
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim(),
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            ProjectType = projectType,
            Status = ProjectStatus.Setup,
            ContractCurrency = contractCurrency.Trim(),
            ClientPartyId = clientPartyId,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            OriginType = ProjectOriginType.Manual
        };
    }

    // planned_end_date >= planned_start_date when both are present (Spec 02 §4.2.1, BR-COR-11).
    private static void EnsureDatesValid(DateOnly? start, DateOnly? end)
    {
        if (start is not null && end is not null && end < start)
            throw new DomainRuleViolation("core.project_dates_invalid");
    }
}
