using Construction.Modules;
using Construction.Procurement;
using Construction.Docs;
using Construction.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Worker;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddModule<PlatformModule>(config);
        services.AddModule<ProcurementModule>(config);
        services.AddModule<DocsModule>(config);
        return services;
    }
}
