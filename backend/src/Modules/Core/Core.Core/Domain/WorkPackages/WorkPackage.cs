using Construction.Core.Core.Domain.Projects;
using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Core.Core.Domain.WorkPackages;

/// <summary>
/// Work package aggregate (Spec 02 §4.3.4: core.work_packages).
/// Lifecycle follows §5.2; business rule BR-COR-19.
/// Guards that need other data (uniqueness per trade and zone, usage providers,
/// the project's current state) are checked by the application layer.
/// </summary>
public sealed class WorkPackage : ITenantScopedEntity, IAuditableEntity
{
    private WorkPackage() { } // For EF Core

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid ProjectId { get; private set; }
    public Guid TradeId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string NameEn { get; private set; } = default!;
    public ProgressMethod ProgressMethod { get; private set; }
    public string? DefaultUnitCode { get; private set; }
    public bool IsTimeRelated { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public WorkPackageStatus Status { get; private set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Creates a work package inside a project. The initial status follows the project's status
    /// (Spec 02 §5.2): active project -> active, otherwise planned; closed/cancelled projects
    /// reject the creation (BR-COR-19). Code, names and the progress method defaults
    /// (from the trade and zone) are computed by the caller.
    /// </summary>
    public static WorkPackage Create(
        Guid id,
        TenantId tenantId,
        Guid projectId,
        ProjectStatus projectStatus,
        Guid tradeId,
        string code,
        string nameAr,
        string nameEn,
        ProgressMethod progressMethod,
        bool isTimeRelated,
        Guid? zoneId = null,
        Guid? parentId = null,
        string? defaultUnitCode = null,
        Guid? responsibleUserId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(id));

        tenantId.EnsureValid();

        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));

        if (tradeId == Guid.Empty)
            throw new ArgumentException("TradeId cannot be empty.", nameof(tradeId));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Work package code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            throw new DomainRuleViolation("core.work_package_name_required");

        return new WorkPackage
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            TradeId = tradeId,
            ZoneId = zoneId,
            ParentId = parentId,
            Code = code.Trim(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            ProgressMethod = progressMethod,
            DefaultUnitCode = string.IsNullOrWhiteSpace(defaultUnitCode) ? null : defaultUnitCode.Trim(),
            IsTimeRelated = isTimeRelated,
            ResponsibleUserId = responsibleUserId,
            Status = InitialStatusFor(projectStatus)
        };
    }

    private static WorkPackageStatus InitialStatusFor(ProjectStatus projectStatus) => projectStatus switch
    {
        ProjectStatus.Active => WorkPackageStatus.Active,
        ProjectStatus.Draft or ProjectStatus.Setup => WorkPackageStatus.Planned,
        // Not covered by the spec: proposed to behave like a project that has not gone live.
        ProjectStatus.OnHold or ProjectStatus.Closing => WorkPackageStatus.Planned,
        ProjectStatus.Closed or ProjectStatus.Cancelled => throw new DomainRuleViolation("core.project_closed"),
        _ => throw new ArgumentOutOfRangeException(nameof(projectStatus), projectStatus, null)
    };
}
