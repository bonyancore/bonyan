using Construction.Kernel.Tenancy;
using Construction.Persistence.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Construction.Persistence.Tests;

public class TenantScopeTests
{
    [Fact]
    public void SettableTenantContext_InitialState_HasTenantIsFalse_AndCurrentThrows()
    {
        var sut = new SettableTenantContext();

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("No tenant has been set", ex.Message);
    }

    [Fact]
    public void SettableTenantContext_SetTenant_Valid_SetsCurrent_AndHasTenantIsTrue()
    {
        var tenantId = TenantId.New();
        var sut = new SettableTenantContext();

        sut.SetTenant(tenantId);

        Assert.True(sut.HasTenant);
        Assert.Equal(tenantId, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_SetTenant_Twice_ThrowsInvalidOperationException()
    {
        var t1 = TenantId.New();
        var t2 = TenantId.New();
        var sut = new SettableTenantContext();

        sut.SetTenant(t1);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.SetTenant(t2));
        Assert.Contains("immutable", ex.Message);
        Assert.Equal(t1, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_SetTenant_DefaultOrEmptyTenantId_ThrowsInvalidOperationException()
    {
        var sut = new SettableTenantContext();

        Assert.Throws<InvalidOperationException>(() => sut.SetTenant(default));
    }

    [Fact]
    public void SettableTenantContext_BeginScope_SetsCurrent_AndRestoresOnDispose()
    {
        var sut = new SettableTenantContext();
        var initialTenant = TenantId.New();
        sut.SetTenant(initialTenant);

        var scopedTenant = TenantId.New();
        using (sut.BeginScope(scopedTenant))
        {
            Assert.True(sut.HasTenant);
            Assert.Equal(scopedTenant, sut.Current);
        }

        // Restored to initial tenant on dispose
        Assert.True(sut.HasTenant);
        Assert.Equal(initialTenant, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_BeginScope_NestedScopes_RestoresCorrectly()
    {
        var sut = new SettableTenantContext();
        var tenant1 = TenantId.New();
        var tenant2 = TenantId.New();
        var tenant3 = TenantId.New();

        sut.SetTenant(tenant1);

        using (sut.BeginScope(tenant2))
        {
            Assert.Equal(tenant2, sut.Current);

            using (sut.BeginScope(tenant3))
            {
                Assert.Equal(tenant3, sut.Current);
            }

            Assert.Equal(tenant2, sut.Current);
        }

        Assert.Equal(tenant1, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_BeginScope_ExceptionThrown_RestoresOnDispose()
    {
        var sut = new SettableTenantContext();
        var initialTenant = TenantId.New();
        sut.SetTenant(initialTenant);

        var scopedTenant = TenantId.New();

        try
        {
            using (sut.BeginScope(scopedTenant))
            {
                Assert.Equal(scopedTenant, sut.Current);
                throw new InvalidOperationException("Simulated failure inside scope");
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        Assert.True(sut.HasTenant);
        Assert.Equal(initialTenant, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_BeginScope_DisposedTwice_IsSafe()
    {
        var sut = new SettableTenantContext();
        var initialTenant = TenantId.New();
        sut.SetTenant(initialTenant);

        var scopedTenant = TenantId.New();
        var scope = sut.BeginScope(scopedTenant);
        Assert.Equal(scopedTenant, sut.Current);

        scope.Dispose();
        Assert.Equal(initialTenant, sut.Current);

        // Second dispose should be a no-op and not throw
        scope.Dispose();
        Assert.Equal(initialTenant, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_BeginScope_DefaultOrEmptyTenantId_ThrowsInvalidOperationException()
    {
        var sut = new SettableTenantContext();

        Assert.Throws<InvalidOperationException>(() => sut.BeginScope(default));
    }

    [Fact]
    public void CreateTenantScope_ConfiguresScopeWithTenant_AndIsolatesBetweenScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<SettableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<SettableTenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<SettableTenantContext>());

        using var rootProvider = services.BuildServiceProvider();

        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        using (var scopeA = rootProvider.CreateTenantScope(tenantA))
        {
            var ctxA = scopeA.ServiceProvider.GetRequiredService<ITenantContext>();
            Assert.True(ctxA.HasTenant);
            Assert.Equal(tenantA, ctxA.Current);

            using (var scopeB = rootProvider.CreateTenantScope(tenantB))
            {
                var ctxB = scopeB.ServiceProvider.GetRequiredService<ITenantContext>();
                Assert.True(ctxB.HasTenant);
                Assert.Equal(tenantB, ctxB.Current);
            }

            // Scope A remains unchanged after Scope B disposal
            Assert.True(ctxA.HasTenant);
            Assert.Equal(tenantA, ctxA.Current);
        }
    }

    [Fact]
    public void CreateTenantScope_WhenTenantInvalid_ThrowsBeforeCreatingScope_AndDoesNotLeak()
    {
        var services = new ServiceCollection();
        services.AddScoped<SettableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<SettableTenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<SettableTenantContext>());

        using var rootProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => rootProvider.CreateTenantScope(default));
    }
}
