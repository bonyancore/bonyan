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
}
