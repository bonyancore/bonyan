using Construction.Core.Core.Domain.Projects;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public sealed class ProjectClosingTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 12, 31);
    private static readonly DateTimeOffset Now = new(2026, 12, 31, 8, 0, 0, TimeSpan.Zero);

    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    private Project NewSetup() =>
        Project.Create(
            Guid.NewGuid(), _tenantId, "P-2026-001", "مشروع", ProjectType.Residential, "EGP",
            clientPartyId: Guid.NewGuid(), plannedStartDate: Start, plannedEndDate: End);

    private Project NewActive()
    {
        var project = NewSetup();
        project.Activate(Start, Now);
        return project;
    }

    private Project NewClosing()
    {
        var project = NewActive();
        project.BeginClosing();
        return project;
    }

    private Project NewClosed()
    {
        var project = NewClosing();
        project.Close(End, Now);
        return project;
    }

    private static void AssertInvalidTransition(Action action)
    {
        var ex = Assert.Throws<DomainRuleViolation>(action);
        Assert.Equal("core.project_invalid_transition", ex.Code);
    }

    // ---- Close

    [Fact]
    public void Close_FromClosing_SetsClosedAndStampsEndDateAndTime()
    {
        var project = NewClosing();

        project.Close(End, Now);

        Assert.Equal(ProjectStatus.Closed, project.Status);
        Assert.Equal(End, project.ActualEndDate);
        Assert.Equal(Now, project.ClosedAt);
        Assert.Null(project.CloseReason);
    }

    [Fact]
    public void Close_WithReason_StoresTrimmedReason()
    {
        var project = NewClosing();

        project.Close(End, Now, "  handover signed with open snag list  ");

        Assert.Equal("handover signed with open snag list", project.CloseReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Close_WithBlankReason_StoresNull(string reason)
    {
        var project = NewClosing();

        project.Close(End, Now, reason);

        Assert.Null(project.CloseReason);
    }

    [Fact]
    public void Close_FromActive_ThrowsInvalidTransition()
    {
        var project = NewActive();

        AssertInvalidTransition(() => project.Close(End, Now));
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    // ---- Reopen

    [Fact]
    public void Reopen_FromClosed_MovesToClosingAndClearsClosedData()
    {
        var project = NewClosing();
        project.Close(End, Now, "handover signed");

        project.Reopen("client found defects");

        Assert.Equal(ProjectStatus.Closing, project.Status);
        Assert.Null(project.ActualEndDate);
        Assert.Null(project.ClosedAt);
        Assert.Null(project.CloseReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reopen_WithBlankReason_ThrowsReasonRequired(string reason)
    {
        var project = NewClosed();

        var ex = Assert.Throws<DomainRuleViolation>(() => project.Reopen(reason));

        Assert.Equal("core.project_reason_required", ex.Code);
        Assert.Equal(ProjectStatus.Closed, project.Status);
    }

    [Fact]
    public void Reopen_FromActive_ThrowsInvalidTransition()
    {
        var project = NewActive();

        AssertInvalidTransition(() => project.Reopen("reason"));
    }

    // ---- Terminal states

    [Fact]
    public void Closed_RejectsEveryActionExceptReopen()
    {
        var project = NewClosed();

        AssertInvalidTransition(project.StartSetup);
        AssertInvalidTransition(() => project.Activate(Start, Now));
        AssertInvalidTransition(() => project.Hold("reason"));
        AssertInvalidTransition(project.Resume);
        AssertInvalidTransition(project.BeginClosing);
        AssertInvalidTransition(() => project.Close(End, Now));
        AssertInvalidTransition(project.Cancel);
        Assert.Equal(ProjectStatus.Closed, project.Status);
    }

    [Fact]
    public void Cancelled_RejectsEveryAction()
    {
        var project = NewSetup();
        project.Cancel();

        AssertInvalidTransition(project.StartSetup);
        AssertInvalidTransition(() => project.Activate(Start, Now));
        AssertInvalidTransition(() => project.Hold("reason"));
        AssertInvalidTransition(project.Resume);
        AssertInvalidTransition(project.BeginClosing);
        AssertInvalidTransition(() => project.Close(End, Now));
        AssertInvalidTransition(() => project.Reopen("reason"));
        AssertInvalidTransition(project.Cancel);
        Assert.Equal(ProjectStatus.Cancelled, project.Status);
    }
}
