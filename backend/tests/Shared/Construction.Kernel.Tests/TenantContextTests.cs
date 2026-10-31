using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Kernel.Tests;

public class TenantContextTests
{
    [Fact]
    public void TenantId_New_GeneratesVersion7Guid()
    {
        var tenantId = TenantId.New();

        Assert.NotEqual(Guid.Empty, tenantId.Value);
        Assert.Equal(7, tenantId.Value.Version);
        Assert.InRange(tenantId.Value.Variant, 8, 11);
    }

    [Fact]
    public void TenantId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var tenantId = new TenantId(guid);

        Assert.Equal(guid.ToString(), tenantId.ToString());
    }

    [Fact]
    public void TenantId_EqualityIsValueBased()
    {
        var guid = Guid.NewGuid();
        var id1 = new TenantId(guid);
        var id2 = new TenantId(guid);

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }

    private sealed class FakeTenantContext(TenantId? current = null) : ITenantContext
    {
        public bool HasTenant => current.HasValue;
        public TenantId Current => current ?? throw new InvalidOperationException("No tenant in context.");
    }

    [Fact]
    public void TenantContext_WhenTenantPresent_ReturnsTenant()
    {
        var expectedId = TenantId.New();
        ITenantContext context = new FakeTenantContext(expectedId);

        Assert.True(context.HasTenant);
        Assert.Equal(expectedId, context.Current);
    }

    [Fact]
    public void TenantContext_WhenNoTenant_ThrowsOnCurrentAccess()
    {
        ITenantContext context = new FakeTenantContext();

        Assert.False(context.HasTenant);
        Assert.Throws<InvalidOperationException>(() => _ = context.Current);
    }
}
