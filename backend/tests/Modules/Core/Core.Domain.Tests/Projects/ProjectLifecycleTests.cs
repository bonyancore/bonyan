using Construction.Core.Core.Domain.Projects;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public sealed class ProjectLifecycleTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 12, 31);
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    private Project NewSetup(bool withClient = true, bool withDates = true) =>
        Project.Create(
            Guid.NewGuid(), _tenantId, "P-2026-001", "مشروع", ProjectType.Residential, "EGP",
            clientPartyId: withClient ? Guid.NewGuid() : null,
            plannedStartDate: withDates ? Start : null,
            plannedEndDate: withDates ? End : null);

    private Project NewDraft(bool withClient = true, bool withDates = true) =>
        Project.CreateDraft(
            Guid.NewGuid(), _tenantId, "P-2026-001", "مشروع", ProjectType.Residential, "EGP",
            Guid.NewGuid(),
            clientPartyId: withClient ? Guid.NewGuid() : null,
            plannedStartDate: withDates ? Start : null,
            plannedEndDate: withDates ? End : null);

    private Project NewActive()
    {
        var project = NewSetup();
        project.Activate(Start, Now);
        return project;
    }

    private static void AssertViolation(string code, Action action)
    {
        var ex = Assert.Throws<DomainRuleViolation>(action);
        Assert.Equal(code, ex.Code);
    }

    // ---- CreateDraft

    [Fact]
    public void CreateDraft_SetsDraftStatusAndTenderOrigin()
    {
        var tenderId = Guid.NewGuid();

        var project = Project.CreateDraft(
            Guid.NewGuid(), _tenantId, "P-1", "مشروع", ProjectType.Mixed, "EGP", tenderId);

        Assert.Equal(ProjectStatus.Draft, project.Status);
        Assert.Equal(ProjectOriginType.Tender, project.OriginType);
        Assert.Equal(tenderId, project.OriginId);
    }

    [Fact]
    public void CreateDraft_WithEmptyTenderId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Project.CreateDraft(Guid.NewGuid(), _tenantId, "P-1", "مشروع", ProjectType.Mixed, "EGP", Guid.Empty));

        Assert.Equal("tenderId", ex.ParamName);
    }

    // ---- StartSetup

    [Fact]
    public void StartSetup_FromDraftWithClientAndDates_MovesToSetup()
    {
        var project = NewDraft();

        project.StartSetup();

        Assert.Equal(ProjectStatus.Setup, project.Status);
    }

    [Fact]
    public void StartSetup_WithoutClient_ThrowsClientRequired()
    {
        var project = NewDraft(withClient: false);

        AssertViolation("core.project_client_required", project.StartSetup);
        Assert.Equal(ProjectStatus.Draft, project.Status);
    }

    [Fact]
    public void StartSetup_WithoutDates_ThrowsDatesInvalid()
    {
        var project = NewDraft(withDates: false);

        AssertViolation("core.project_dates_invalid", project.StartSetup);
        Assert.Equal(ProjectStatus.Draft, project.Status);
    }

    [Fact]
    public void StartSetup_FromSetup_ThrowsInvalidTransition()
    {
        var project = NewSetup();

        AssertViolation("core.project_invalid_transition", project.StartSetup);
    }

    // ---- Activate

    [Fact]
    public void Activate_FromSetup_SetsActiveAndStampsStart()
    {
        var project = NewSetup();

        project.Activate(Start, Now);

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(Start, project.ActualStartDate);
        Assert.Equal(Now, project.ActivatedAt);
    }

    [Fact]
    public void Activate_WithoutClient_ThrowsClientRequired()
    {
        var project = NewSetup(withClient: false);

        AssertViolation("core.project_client_required", () => project.Activate(Start, Now));
        Assert.Equal(ProjectStatus.Setup, project.Status);
    }

    [Fact]
    public void Activate_WithoutDates_ThrowsDatesInvalid()
    {
        var project = NewSetup(withDates: false);

        AssertViolation("core.project_dates_invalid", () => project.Activate(Start, Now));
    }

    [Fact]
    public void Activate_FromOnHold_ThrowsInvalidTransition()
    {
        var project = NewActive();
        project.Hold("waiting for the client");

        AssertViolation("core.project_invalid_transition", () => project.Activate(Start, Now));
        Assert.Equal(ProjectStatus.OnHold, project.Status);
    }

    // ---- Hold / Resume

    [Fact]
    public void Hold_FromActive_SetsOnHoldWithTrimmedReason()
    {
        var project = NewActive();

        project.Hold("  waiting for the client  ");

        Assert.Equal(ProjectStatus.OnHold, project.Status);
        Assert.Equal("waiting for the client", project.HoldReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hold_WithBlankReason_ThrowsReasonRequired(string reason)
    {
        var project = NewActive();

        AssertViolation("core.project_reason_required", () => project.Hold(reason));
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void Hold_FromSetup_ThrowsInvalidTransition()
    {
        var project = NewSetup();

        AssertViolation("core.project_invalid_transition", () => project.Hold("reason"));
    }

    [Fact]
    public void Resume_FromOnHold_ReturnsToActiveAndClearsReason()
    {
        var project = NewActive();
        project.Hold("waiting for the client");

        project.Resume();

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Null(project.HoldReason);
    }

    [Fact]
    public void Resume_FromActive_ThrowsInvalidTransition()
    {
        var project = NewActive();

        AssertViolation("core.project_invalid_transition", project.Resume);
    }

    // ---- BeginClosing

    [Fact]
    public void BeginClosing_FromActive_MovesToClosing()
    {
        var project = NewActive();

        project.BeginClosing();

        Assert.Equal(ProjectStatus.Closing, project.Status);
    }

    [Fact]
    public void BeginClosing_FromSetup_ThrowsInvalidTransition()
    {
        var project = NewSetup();

        AssertViolation("core.project_invalid_transition", project.BeginClosing);
    }

    // ---- Cancel

    [Fact]
    public void Cancel_FromDraft_MovesToCancelled()
    {
        var project = NewDraft();

        project.Cancel();

        Assert.Equal(ProjectStatus.Cancelled, project.Status);
    }

    [Fact]
    public void Cancel_FromSetup_MovesToCancelled()
    {
        var project = NewSetup();

        project.Cancel();

        Assert.Equal(ProjectStatus.Cancelled, project.Status);
    }

    [Fact]
    public void Cancel_FromActive_ThrowsInvalidTransition()
    {
        var project = NewActive();

        AssertViolation("core.project_invalid_transition", project.Cancel);
    }
}
