namespace Construction.Core.Core.Domain.WorkPackages;

/// <summary>
/// Allowed work package status transitions (Spec 02 §5.2).
/// Pure rules only: guards that need other data (open requests, project state)
/// belong to the application layer.
/// </summary>
public static class WorkPackageTransitions
{
    public static bool CanTransition(WorkPackageStatus from, WorkPackageStatus to) => (from, to) switch
    {
        (WorkPackageStatus.Planned, WorkPackageStatus.Active) => true,       // activate
        (WorkPackageStatus.Active, WorkPackageStatus.OnHold) => true,        // hold
        (WorkPackageStatus.OnHold, WorkPackageStatus.Active) => true,        // resume
        (WorkPackageStatus.Active, WorkPackageStatus.Completed) => true,     // complete
        (WorkPackageStatus.Completed, WorkPackageStatus.Active) => true,     // reopen
        (WorkPackageStatus.Planned, WorkPackageStatus.Archived) => true,     // archive
        (WorkPackageStatus.OnHold, WorkPackageStatus.Archived) => true,      // archive
        (WorkPackageStatus.Completed, WorkPackageStatus.Archived) => true,   // archive
        (WorkPackageStatus.Archived, WorkPackageStatus.Planned) => true,     // unarchive
        _ => false
    };
}
