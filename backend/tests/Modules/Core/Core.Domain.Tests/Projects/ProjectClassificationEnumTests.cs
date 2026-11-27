using Construction.Core.Core.Domain.Projects;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public sealed class ProjectClassificationEnumTests
{
    [Theory]
    [InlineData(ProjectType.Residential, "residential")]
    [InlineData(ProjectType.Commercial, "commercial")]
    [InlineData(ProjectType.Infrastructure, "infrastructure")]
    [InlineData(ProjectType.FitOut, "fit_out")]
    [InlineData(ProjectType.Industrial, "industrial")]
    [InlineData(ProjectType.Mixed, "mixed")]
    public void ProjectType_ToDbValue_And_Parse_Roundtrip(ProjectType type, string dbValue)
    {
        Assert.Equal(dbValue, type.ToDbValue());
        Assert.Equal(type, ProjectEnumExtensions.ParseProjectType(dbValue));
    }

    [Theory]
    [InlineData(ContractType.LumpSum, "lump_sum")]
    [InlineData(ContractType.Remeasurable, "remeasurable")]
    [InlineData(ContractType.CostPlus, "cost_plus")]
    [InlineData(ContractType.DesignBuild, "design_build")]
    [InlineData(ContractType.Other, "other")]
    public void ContractType_ToDbValue_And_Parse_Roundtrip(ContractType type, string dbValue)
    {
        Assert.Equal(dbValue, type.ToDbValue());
        Assert.Equal(type, ProjectEnumExtensions.ParseContractType(dbValue));
    }

    [Theory]
    [InlineData(ProjectOriginType.Manual, "manual")]
    [InlineData(ProjectOriginType.Tender, "tender")]
    [InlineData(ProjectOriginType.Import, "import")]
    public void ProjectOriginType_ToDbValue_And_Parse_Roundtrip(ProjectOriginType origin, string dbValue)
    {
        Assert.Equal(dbValue, origin.ToDbValue());
        Assert.Equal(origin, ProjectEnumExtensions.ParseProjectOriginType(dbValue));
    }

    [Fact]
    public void Parse_UnknownValues_Throw()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ProjectEnumExtensions.ParseProjectType("villa"));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ProjectEnumExtensions.ParseContractType("fixed"));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ProjectEnumExtensions.ParseProjectOriginType("api"));
    }
}
