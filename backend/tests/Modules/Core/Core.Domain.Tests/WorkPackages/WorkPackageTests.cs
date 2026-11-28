using Construction.Core.Core.Domain.Projects;
using Construction.Core.Core.Domain.WorkPackages;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Core.Domain.Tests.WorkPackages;

public sealed class WorkPackageTests
{
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    private WorkPackage CreateValid(
        ProjectStatus projectStatus = ProjectStatus.Setup,
        string nameAr = "أعمال السباكة",
        string nameEn = "Plumbing works") =>
        WorkPackage.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), projectStatus, Guid.NewGuid(),
            "B3-07", nameAr, nameEn, ProgressMethod.Quantity, isTimeRelated: false);

    [Fact]
    public void Create_WithValidData_SetsAllFields()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var responsible = Guid.NewGuid();

        var wp = WorkPackage.Create(
            id, _tenantId, projectId, ProjectStatus.Setup, tradeId,
            " B3-07 ", "  أعمال السباكة ", " Plumbing works ", ProgressMethod.Stage, isTimeRelated: true,
            zoneId: zoneId, defaultUnitCode: " m ", responsibleUserId: responsible);

        Assert.Equal(id, wp.Id);
        Assert.Equal(_tenantId, wp.TenantId);
        Assert.Equal(projectId, wp.ProjectId);
        Assert.Equal(tradeId, wp.TradeId);
        Assert.Equal(zoneId, wp.ZoneId);
        Assert.Null(wp.ParentId);
        Assert.Equal("B3-07", wp.Code);
        Assert.Equal("أعمال السباكة", wp.NameAr);
        Assert.Equal("Plumbing works", wp.NameEn);
        Assert.Equal(ProgressMethod.Stage, wp.ProgressMethod);
        Assert.Equal("m", wp.DefaultUnitCode);
        Assert.True(wp.IsTimeRelated);
        Assert.Equal(responsible, wp.ResponsibleUserId);
        Assert.Equal(WorkPackageStatus.Planned, wp.Status);
    }

    [Theory]
    [InlineData(ProjectStatus.Draft, WorkPackageStatus.Planned)]
    [InlineData(ProjectStatus.Setup, WorkPackageStatus.Planned)]
    [InlineData(ProjectStatus.Active, WorkPackageStatus.Active)]
    [InlineData(ProjectStatus.OnHold, WorkPackageStatus.Planned)]
    [InlineData(ProjectStatus.Closing, WorkPackageStatus.Planned)]
    public void Create_InitialStatusFollowsProjectStatus(ProjectStatus projectStatus, WorkPackageStatus expected)
    {
        var wp = CreateValid(projectStatus);

        Assert.Equal(expected, wp.Status);
    }

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Cancelled)]
    public void Create_InClosedOrCancelledProject_ThrowsProjectClosed(ProjectStatus projectStatus)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() => CreateValid(projectStatus));

        Assert.Equal("core.project_closed", ex.Code);
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WorkPackage.Create(Guid.Empty, _tenantId, Guid.NewGuid(), ProjectStatus.Setup, Guid.NewGuid(),
                "B3-07", "سباكة", "Plumbing", ProgressMethod.Quantity, false));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WorkPackage.Create(Guid.NewGuid(), _tenantId, Guid.Empty, ProjectStatus.Setup, Guid.NewGuid(),
                "B3-07", "سباكة", "Plumbing", ProgressMethod.Quantity, false));

        Assert.Equal("projectId", ex.ParamName);
    }

    [Fact]
    public void Create_WithEmptyTradeId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WorkPackage.Create(Guid.NewGuid(), _tenantId, Guid.NewGuid(), ProjectStatus.Setup, Guid.Empty,
                "B3-07", "سباكة", "Plumbing", ProgressMethod.Quantity, false));

        Assert.Equal("tradeId", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ThrowsArgumentException(string code)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WorkPackage.Create(Guid.NewGuid(), _tenantId, Guid.NewGuid(), ProjectStatus.Setup, Guid.NewGuid(),
                code, "سباكة", "Plumbing", ProgressMethod.Quantity, false));

        Assert.Equal("code", ex.ParamName);
    }

    [Theory]
    [InlineData("", "Plumbing")]
    [InlineData("   ", "Plumbing")]
    [InlineData("سباكة", "")]
    [InlineData("سباكة", "   ")]
    public void Create_WithBlankName_ThrowsNameRequired(string nameAr, string nameEn)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() => CreateValid(nameAr: nameAr, nameEn: nameEn));

        Assert.Equal("core.work_package_name_required", ex.Code);
    }
}
