namespace Construction.Core.Core.Domain.Projects;

/// <summary>
/// Lifecycle status of a project (Spec 02 §4.2.6, §5.1).
/// Allowed values: 'draft', 'setup', 'active', 'on_hold', 'closing', 'closed', 'cancelled'.
/// </summary>
public enum ProjectStatus
{
    Draft,
    Setup,
    Active,
    OnHold,
    Closing,
    Closed,
    Cancelled
}

/// <summary>
/// Kind of project (Spec 02 §4.2.1).
/// Allowed values: 'residential', 'commercial', 'infrastructure', 'fit_out', 'industrial', 'mixed'.
/// </summary>
public enum ProjectType
{
    Residential,
    Commercial,
    Infrastructure,
    FitOut,
    Industrial,
    Mixed
}

/// <summary>
/// Contract type of a project (Spec 02 §4.2.1).
/// Allowed values: 'lump_sum', 'remeasurable', 'cost_plus', 'design_build', 'other'.
/// </summary>
public enum ContractType
{
    LumpSum,
    Remeasurable,
    CostPlus,
    DesignBuild,
    Other
}

/// <summary>
/// How a project came to exist (Spec 02 §4.2.1, BR-COR-13).
/// Allowed values: 'manual', 'tender', 'import'.
/// </summary>
public enum ProjectOriginType
{
    Manual,
    Tender,
    Import
}

public static class ProjectEnumExtensions
{
    public static string ToDbValue(this ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "draft",
        ProjectStatus.Setup => "setup",
        ProjectStatus.Active => "active",
        ProjectStatus.OnHold => "on_hold",
        ProjectStatus.Closing => "closing",
        ProjectStatus.Closed => "closed",
        ProjectStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ProjectStatus ParseProjectStatus(string value) => value.ToLowerInvariant() switch
    {
        "draft" => ProjectStatus.Draft,
        "setup" => ProjectStatus.Setup,
        "active" => ProjectStatus.Active,
        "on_hold" => ProjectStatus.OnHold,
        "closing" => ProjectStatus.Closing,
        "closed" => ProjectStatus.Closed,
        "cancelled" => ProjectStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown ProjectStatus: {value}")
    };

    public static string ToDbValue(this ProjectType type) => type switch
    {
        ProjectType.Residential => "residential",
        ProjectType.Commercial => "commercial",
        ProjectType.Infrastructure => "infrastructure",
        ProjectType.FitOut => "fit_out",
        ProjectType.Industrial => "industrial",
        ProjectType.Mixed => "mixed",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static ProjectType ParseProjectType(string value) => value.ToLowerInvariant() switch
    {
        "residential" => ProjectType.Residential,
        "commercial" => ProjectType.Commercial,
        "infrastructure" => ProjectType.Infrastructure,
        "fit_out" => ProjectType.FitOut,
        "industrial" => ProjectType.Industrial,
        "mixed" => ProjectType.Mixed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown ProjectType: {value}")
    };

    public static string ToDbValue(this ContractType type) => type switch
    {
        ContractType.LumpSum => "lump_sum",
        ContractType.Remeasurable => "remeasurable",
        ContractType.CostPlus => "cost_plus",
        ContractType.DesignBuild => "design_build",
        ContractType.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static ContractType ParseContractType(string value) => value.ToLowerInvariant() switch
    {
        "lump_sum" => ContractType.LumpSum,
        "remeasurable" => ContractType.Remeasurable,
        "cost_plus" => ContractType.CostPlus,
        "design_build" => ContractType.DesignBuild,
        "other" => ContractType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown ContractType: {value}")
    };

    public static string ToDbValue(this ProjectOriginType origin) => origin switch
    {
        ProjectOriginType.Manual => "manual",
        ProjectOriginType.Tender => "tender",
        ProjectOriginType.Import => "import",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
    };

    public static ProjectOriginType ParseProjectOriginType(string value) => value.ToLowerInvariant() switch
    {
        "manual" => ProjectOriginType.Manual,
        "tender" => ProjectOriginType.Tender,
        "import" => ProjectOriginType.Import,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown ProjectOriginType: {value}")
    };
}
