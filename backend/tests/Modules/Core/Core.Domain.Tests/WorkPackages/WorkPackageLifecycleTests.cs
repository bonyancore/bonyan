using Construction.Core.Core.Domain.Projects;
using Construction.Core.Core.Domain.WorkPackages;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Core.Domain.Tests.WorkPackages;

public sealed class WorkPackageLifecycleTests
{
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    private WorkPackage NewPlanned() =>
        WorkPackage.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), ProjectStatus.Setup, Guid.NewGuid(),
            "B3-07", "أعمال السباكة", "Plumbing works", ProgressMethod.Quantity, isTimeRelated: false);

    // Builds a work package that is in the requested status, using only legal actions.
    private WorkPackage InStatus(WorkPackageStatus status)
    {
        var wp = NewPlanned();
        switch (status)
        {
            case WorkPackageStatus.Planned:
                break;
            case WorkPackageStatus.Active:
                wp.Activate();
                break;
            case WorkPackageStatus.OnHold:
                wp.Activate();
                wp.Hold("waiting for materials");
                break;
            case WorkPackageStatus.Completed:
                wp.Activate();
                wp.Complete();
                break;
            case WorkPackageStatus.Archived:
                wp.Archive();
                break;
        }

        Assert.Equal(status, wp.Status);
        return wp;
    }

    private static void AssertInvalidTransition(WorkPackage wp, Action action)
    {
        var before = wp.Status;

        var ex = Assert.Throws<DomainRuleViolation>(action);

        Assert.Equal("core.work_package_invalid_transition", ex.Code);
        Assert.Equal(before, wp.Status);
    }

    // ---- Activate

    [Fact]
    public void Activate_FromPlanned_MovesToActive()
    {
        var wp = InStatus(WorkPackageStatus.Planned);

        wp.Activate();

        Assert.Equal(WorkPackageStatus.Active, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Activate_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Activate);
    }

    // ---- Hold

    [Fact]
    public void Hold_FromActive_MovesToOnHold()
    {
        var wp = InStatus(WorkPackageStatus.Active);

        wp.Hold("waiting for materials");

        Assert.Equal(WorkPackageStatus.OnHold, wp.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hold_WithBlankReason_ThrowsReasonRequired(string reason)
    {
        var wp = InStatus(WorkPackageStatus.Active);

        var ex = Assert.Throws<DomainRuleViolation>(() => wp.Hold(reason));

        Assert.Equal("core.work_package_reason_required", ex.Code);
        Assert.Equal(WorkPackageStatus.Active, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Hold_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, () => wp.Hold("reason"));
    }

    // ---- Resume

    [Fact]
    public void Resume_FromOnHold_MovesToActive()
    {
        var wp = InStatus(WorkPackageStatus.OnHold);

        wp.Resume();

        Assert.Equal(WorkPackageStatus.Active, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Resume_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Resume);
    }

    // ---- Complete

    [Fact]
    public void Complete_FromActive_MovesToCompleted()
    {
        var wp = InStatus(WorkPackageStatus.Active);

        wp.Complete();

        Assert.Equal(WorkPackageStatus.Completed, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Complete_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Complete);
    }

    // ---- Reopen

    [Fact]
    public void Reopen_FromCompleted_MovesToActive()
    {
        var wp = InStatus(WorkPackageStatus.Completed);

        wp.Reopen();

        Assert.Equal(WorkPackageStatus.Active, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Reopen_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Reopen);
    }

    // ---- Archive

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed)]
    public void Archive_FromAllowedStatuses_MovesToArchived(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        wp.Archive();

        Assert.Equal(WorkPackageStatus.Archived, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Archived)]
    public void Archive_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Archive);
    }

    // ---- Unarchive

    [Fact]
    public void Unarchive_FromArchived_MovesToPlanned()
    {
        var wp = InStatus(WorkPackageStatus.Archived);

        wp.Unarchive();

        Assert.Equal(WorkPackageStatus.Planned, wp.Status);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed)]
    public void Unarchive_FromOtherStatuses_ThrowsInvalidTransition(WorkPackageStatus status)
    {
        var wp = InStatus(status);

        AssertInvalidTransition(wp, wp.Unarchive);
    }
}
