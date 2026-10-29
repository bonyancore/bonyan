using Construction.Platform.Infrastructure;

namespace Construction.Worker;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        return services;
    }
}
