using Construction.Core.Core.Domain.Projects;
using Xunit;

namespace Construction.Core.Domain.Tests.Projects;

public class ProjectEnumTests
{
    [Theory]
    [InlineData(ProjectStatus.Draft, "draft")]
    [InlineData(ProjectStatus.Setup, "setup")]
    [InlineData(ProjectStatus.Active, "active")]
    [InlineData(ProjectStatus.OnHold, "on_hold")]
    [InlineData(ProjectStatus.Closing, "closing")]
    [InlineData(ProjectStatus.Closed, "closed")]
    [InlineData(ProjectStatus.Cancelled, "cancelled")]
    public void ProjectStatus_ToDbValue_And_Parse_Roundtrip(ProjectStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, ProjectEnumExtensions.ParseProjectStatus(dbValue));
    }

    [Fact]
    public void ParseProjectStatus_UnknownValue_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => ProjectEnumExtensions.ParseProjectStatus("archived"));
    }
}
