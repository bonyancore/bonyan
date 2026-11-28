using Construction.Core.Core.Domain.Projects;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public sealed class ProjectTransitionsTests
{
    [Theory]
    [InlineData(ProjectStatus.Draft, ProjectStatus.Setup)]
    [InlineData(ProjectStatus.Setup, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Active, ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.OnHold, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Closing)]
    [InlineData(ProjectStatus.Closing, ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Closed, ProjectStatus.Closing)]
    [InlineData(ProjectStatus.Draft, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Setup, ProjectStatus.Cancelled)]
    public void CanTransition_AllowedTransitions_ReturnTrue(ProjectStatus from, ProjectStatus to)
    {
        Assert.True(ProjectTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ProjectStatus.Closed, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Cancelled, ProjectStatus.Setup)]
    [InlineData(ProjectStatus.Cancelled, ProjectStatus.Draft)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Setup, ProjectStatus.Draft)]
    [InlineData(ProjectStatus.Closing, ProjectStatus.Active)]
    [InlineData(ProjectStatus.Active, ProjectStatus.Setup)]
    public void CanTransition_ForbiddenTransitions_ReturnFalse(ProjectStatus from, ProjectStatus to)
    {
        Assert.False(ProjectTransitions.CanTransition(from, to));
    }

    [Fact]
    public void Cancelled_IsTerminal()
    {
        foreach (var to in Enum.GetValues<ProjectStatus>())
        {
            Assert.False(ProjectTransitions.CanTransition(ProjectStatus.Cancelled, to));
        }
    }

    [Fact]
    public void Closed_CanOnlyMoveToClosing()
    {
        foreach (var to in Enum.GetValues<ProjectStatus>())
        {
            var expected = to == ProjectStatus.Closing;
            Assert.Equal(expected, ProjectTransitions.CanTransition(ProjectStatus.Closed, to));
        }
    }

    [Fact]
    public void SameStatus_IsNotATransition()
    {
        foreach (var status in Enum.GetValues<ProjectStatus>())
        {
            Assert.False(ProjectTransitions.CanTransition(status, status));
        }
    }
}
