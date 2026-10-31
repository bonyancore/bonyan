using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Persistence.Tests;

public class TenantIdTests
{
    [Fact]
    public void Constructor_WithGuidEmpty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
        Assert.Contains("TenantId cannot be empty", ex.Message);
    }

    [Fact]
    public void From_WithGuidEmpty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => TenantId.From(Guid.Empty));
        Assert.Contains("TenantId cannot be empty", ex.Message);
    }

    [Fact]
    public void From_WithValidGuid_CreatesTenantId()
    {
        var guid = Guid.NewGuid();
        var tenantId = TenantId.From(guid);

        Assert.Equal(guid, tenantId.Value);
        Assert.True(tenantId.IsValid);
    }

    [Fact]
    public void DefaultStruct_IsValid_ReturnsFalse()
    {
        var defaultTenant = default(TenantId);

        Assert.False(defaultTenant.IsValid);
        Assert.Equal(Guid.Empty, defaultTenant.Value);
    }

    [Fact]
    public void DefaultStruct_EnsureValid_ThrowsInvalidOperationException()
    {
        var defaultTenant = default(TenantId);

        var ex = Assert.Throws<InvalidOperationException>(() => defaultTenant.EnsureValid());
        Assert.Contains("TenantId is not initialized", ex.Message);
    }

    [Fact]
    public void Deconstruct_And_WithExpression_WorkAsExpected()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();

        var t1 = new TenantId(g1);
        t1.Deconstruct(out var val);
        Assert.Equal(g1, val);

        var t2 = t1 with { Value = g2 };
        Assert.Equal(g2, t2.Value);
    }
}
