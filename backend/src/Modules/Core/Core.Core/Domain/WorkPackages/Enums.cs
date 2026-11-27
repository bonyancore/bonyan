namespace Construction.Core.Core.Domain.WorkPackages;

/// <summary>
/// Lifecycle status of a work package (Spec 02 §5.2).
/// Allowed values: 'planned', 'active', 'on_hold', 'completed', 'archived'.
/// </summary>
public enum WorkPackageStatus
{
    Planned,
    Active,
    OnHold,
    Completed,
    Archived
}

public static class WorkPackageEnumExtensions
{
    public static string ToDbValue(this WorkPackageStatus status) => status switch
    {
        WorkPackageStatus.Planned => "planned",
        WorkPackageStatus.Active => "active",
        WorkPackageStatus.OnHold => "on_hold",
        WorkPackageStatus.Completed => "completed",
        WorkPackageStatus.Archived => "archived",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static WorkPackageStatus ParseWorkPackageStatus(string value) => value.ToLowerInvariant() switch
    {
        "planned" => WorkPackageStatus.Planned,
        "active" => WorkPackageStatus.Active,
        "on_hold" => WorkPackageStatus.OnHold,
        "completed" => WorkPackageStatus.Completed,
        "archived" => WorkPackageStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown WorkPackageStatus: {value}")
    };
}
