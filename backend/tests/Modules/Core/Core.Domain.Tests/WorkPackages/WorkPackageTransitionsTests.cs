using Construction.Core.Core.Domain.WorkPackages;
using Xunit;

namespace Construction.Core.Domain.Tests.WorkPackages;

public sealed class WorkPackageTransitionsTests
{
    [Theory]
    [InlineData(WorkPackageStatus.Planned, WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.OnHold, WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Completed, WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Planned, WorkPackageStatus.Archived)]
    [InlineData(WorkPackageStatus.OnHold, WorkPackageStatus.Archived)]
    [InlineData(WorkPackageStatus.Completed, WorkPackageStatus.Archived)]
    [InlineData(WorkPackageStatus.Archived, WorkPackageStatus.Planned)]
    public void CanTransition_AllowedTransitions_ReturnTrue(WorkPackageStatus from, WorkPackageStatus to)
    {
        Assert.True(WorkPackageTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.Archived)]
    [InlineData(WorkPackageStatus.Planned, WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.Planned, WorkPackageStatus.OnHold)]
    [InlineData(WorkPackageStatus.Completed, WorkPackageStatus.Planned)]
    [InlineData(WorkPackageStatus.Archived, WorkPackageStatus.Active)]
    [InlineData(WorkPackageStatus.Archived, WorkPackageStatus.Completed)]
    [InlineData(WorkPackageStatus.OnHold, WorkPackageStatus.Completed)]
    public void CanTransition_ForbiddenTransitions_ReturnFalse(WorkPackageStatus from, WorkPackageStatus to)
    {
        Assert.False(WorkPackageTransitions.CanTransition(from, to));
    }

    [Fact]
    public void Archived_CanOnlyMoveToPlanned()
    {
        foreach (var to in Enum.GetValues<WorkPackageStatus>())
        {
            var expected = to == WorkPackageStatus.Planned;
            Assert.Equal(expected, WorkPackageTransitions.CanTransition(WorkPackageStatus.Archived, to));
        }
    }

    [Fact]
    public void SameStatus_IsNotATransition()
    {
        foreach (var status in Enum.GetValues<WorkPackageStatus>())
        {
            Assert.False(WorkPackageTransitions.CanTransition(status, status));
        }
    }
}
