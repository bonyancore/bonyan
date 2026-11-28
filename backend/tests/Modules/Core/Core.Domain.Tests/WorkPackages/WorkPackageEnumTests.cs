using Construction.Core.Core.Domain.WorkPackages;
using Xunit;

namespace Construction.Core.Domain.Tests.WorkPackages;

public sealed class WorkPackageEnumTests
{
    [Theory]
    [InlineData(WorkPackageStatus.Planned, "planned")]
    [InlineData(WorkPackageStatus.Active, "active")]
    [InlineData(WorkPackageStatus.OnHold, "on_hold")]
    [InlineData(WorkPackageStatus.Completed, "completed")]
    [InlineData(WorkPackageStatus.Archived, "archived")]
    public void WorkPackageStatus_ToDbValue_And_Parse_Roundtrip(WorkPackageStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, WorkPackageEnumExtensions.ParseWorkPackageStatus(dbValue));
    }

    [Fact]
    public void ParseWorkPackageStatus_UnknownValue_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => WorkPackageEnumExtensions.ParseWorkPackageStatus("closed"));
    }
}
