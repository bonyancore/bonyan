using Construction.Core.Core.Domain.WorkPackages;
using Xunit;

namespace Construction.Core.Domain.Tests.WorkPackages;

public sealed class ProgressMethodEnumTests
{
    [Theory]
    [InlineData(ProgressMethod.Quantity, "quantity")]
    [InlineData(ProgressMethod.Stage, "stage")]
    [InlineData(ProgressMethod.Milestone, "milestone")]
    public void ProgressMethod_ToDbValue_And_Parse_Roundtrip(ProgressMethod method, string dbValue)
    {
        Assert.Equal(dbValue, method.ToDbValue());
        Assert.Equal(method, WorkPackageEnumExtensions.ParseProgressMethod(dbValue));
    }

    [Fact]
    public void ParseProgressMethod_UnknownValue_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => WorkPackageEnumExtensions.ParseProgressMethod("weight"));
    }
}
