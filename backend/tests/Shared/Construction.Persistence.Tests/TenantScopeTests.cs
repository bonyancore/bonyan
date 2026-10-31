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
        var setter = (ITenantSetter)sut;

        setter.SetTenant(tenantId);

        Assert.True(sut.HasTenant);
        Assert.Equal(tenantId, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_SetTenant_Twice_ThrowsInvalidOperationException()
    {
        var t1 = TenantId.New();
        var t2 = TenantId.New();
        var sut = new SettableTenantContext();
        var setter = (ITenantSetter)sut;

        setter.SetTenant(t1);

        var ex = Assert.Throws<InvalidOperationException>(() => setter.SetTenant(t2));
        Assert.Contains("immutable", ex.Message);
        Assert.Equal(t1, sut.Current);
    }

    [Fact]
    public void SettableTenantContext_SetTenant_DefaultOrEmptyTenantId_ThrowsInvalidOperationException()
    {
        var sut = new SettableTenantContext();
        var setter = (ITenantSetter)sut;

        Assert.Throws<InvalidOperationException>(() => setter.SetTenant(default));
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

