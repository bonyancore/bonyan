using Construction.Kernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Persistence.Tenancy;

public static class TenantScopeExtensions
{
    public static IServiceScope CreateTenantScope(this IServiceProvider rootProvider, TenantId tenantId)
    {
        tenantId.EnsureValid();

        var scope = rootProvider.CreateScope();
        try
        {
            var setter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
            setter.SetTenant(tenantId);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}

