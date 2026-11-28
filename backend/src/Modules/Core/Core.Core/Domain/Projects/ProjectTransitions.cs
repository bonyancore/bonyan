namespace Construction.Core.Core.Domain.Projects;

/// <summary>
/// Allowed project status transitions (Spec 02 §5.1).
/// Pure rules only: no I/O and no clock. Guards that need other data
/// (readiness, usage checks) belong to the application layer.
/// </summary>
public static class ProjectTransitions
{
    public static bool CanTransition(ProjectStatus from, ProjectStatus to) => (from, to) switch
    {
        (ProjectStatus.Draft, ProjectStatus.Setup) => true,        // start-setup
        (ProjectStatus.Setup, ProjectStatus.Active) => true,       // activate
        (ProjectStatus.Active, ProjectStatus.OnHold) => true,      // hold
        (ProjectStatus.OnHold, ProjectStatus.Active) => true,      // resume
        (ProjectStatus.Active, ProjectStatus.Closing) => true,     // begin-closing
        (ProjectStatus.Closing, ProjectStatus.Closed) => true,     // close
        (ProjectStatus.Closed, ProjectStatus.Closing) => true,     // reopen
        (ProjectStatus.Draft, ProjectStatus.Cancelled) => true,    // cancel
        (ProjectStatus.Setup, ProjectStatus.Cancelled) => true,    // cancel
        _ => false
    };
}
