using Construction.Kernel;
using Construction.Persistence.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Construction.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence infrastructure interceptors and default clock as services in DI.
    /// Scoped interceptors are wired to each module's DbContext options via .AddInterceptors(...).
    /// </summary>
    public static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<TenantSessionInterceptor>();
        return services;
    }
}
