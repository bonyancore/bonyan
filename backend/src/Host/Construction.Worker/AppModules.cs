using Construction.Platform.Infrastructure;
using Construction.Platform.Infrastructure.Jobs.Runner;

namespace Construction.Worker;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(
        this IServiceCollection services,
        IConfiguration config,
        bool allowInMemoryStores)
    {
        services.AddPlatformInfrastructure(config);

        var modules = PlatformInfrastructureRegistration.BindModuleOptions(config);
        if (modules.Platform)
        {
            services.AddPlatformWorkerJobs(config, allowInMemoryStores);
            services.AddHostedService<JobSchedulerHostedService>();
            services.AddHostedService<OutboxDispatcherHostedService>();
        }

        return services;
    }
}
