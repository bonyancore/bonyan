using Construction.Core.Core.Domain.Projects;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public sealed class ProjectTests
{
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    private Project CreateValid(
        string nameAr = "برج النخيل",
        DateOnly? start = null,
        DateOnly? end = null) =>
        Project.Create(
            Guid.NewGuid(), _tenantId, "P-2026-001", nameAr,
            ProjectType.Residential, "EGP",
            plannedStartDate: start, plannedEndDate: end);

    [Fact]
    public void Create_WithValidData_ReturnsProjectInSetup()
    {
        var id = Guid.NewGuid();

        var project = Project.Create(
            id, _tenantId, " P-2026-001 ", "  برج النخيل ", ProjectType.Commercial, "EGP", nameEn: "Palm Tower");

        Assert.Equal(id, project.Id);
        Assert.Equal(_tenantId, project.TenantId);
        Assert.Equal("P-2026-001", project.Code);
        Assert.Equal("برج النخيل", project.NameAr);
        Assert.Equal("Palm Tower", project.NameEn);
        Assert.Equal(ProjectType.Commercial, project.ProjectType);
        Assert.Equal(ProjectStatus.Setup, project.Status);
        Assert.Equal("EGP", project.ContractCurrency);
        Assert.Equal(ProjectOriginType.Manual, project.OriginType);
        Assert.Null(project.ClientPartyId);
        Assert.Null(project.PlannedStartDate);
        Assert.Null(project.PlannedEndDate);
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Project.Create(Guid.Empty, _tenantId, "P-1", "مشروع", ProjectType.Mixed, "EGP"));

        Assert.Equal("id", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCode_ThrowsArgumentException(string code)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Project.Create(Guid.NewGuid(), _tenantId, code, "مشروع", ProjectType.Mixed, "EGP"));

        Assert.Equal("code", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankArabicName_ThrowsProjectNameRequired(string nameAr)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() => CreateValid(nameAr: nameAr));

        Assert.Equal("core.project_name_required", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCurrency_ThrowsCurrencyUnknown(string currency)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() =>
            Project.Create(Guid.NewGuid(), _tenantId, "P-1", "مشروع", ProjectType.Mixed, currency));

        Assert.Equal("core.currency_unknown", ex.Code);
    }

    [Fact]
    public void Create_WithEndBeforeStart_ThrowsDatesInvalid()
    {
        var ex = Assert.Throws<DomainRuleViolation>(() =>
            CreateValid(start: new DateOnly(2026, 6, 1), end: new DateOnly(2026, 5, 31)));

        Assert.Equal("core.project_dates_invalid", ex.Code);
    }

    [Fact]
    public void Create_WithEndEqualToStart_Succeeds()
    {
        var day = new DateOnly(2026, 6, 1);

        var project = CreateValid(start: day, end: day);

        Assert.Equal(day, project.PlannedStartDate);
        Assert.Equal(day, project.PlannedEndDate);
    }

    [Fact]
    public void Create_WithOnlyOneDate_Succeeds()
    {
        var project = CreateValid(start: new DateOnly(2026, 6, 1));

        Assert.NotNull(project.PlannedStartDate);
        Assert.Null(project.PlannedEndDate);
    }
}
