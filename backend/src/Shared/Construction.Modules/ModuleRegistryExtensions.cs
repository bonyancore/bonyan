using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Modules;

public static class ModuleRegistryExtensions
{
    public static IServiceCollection AddModule<TModule>(this IServiceCollection services, IConfiguration config)
        where TModule : class, IModule, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        if (IsModuleRegistered(services, typeof(TModule)))
        {
            return services;
        }

        return services.AddModule(new TModule(), config);
    }

    public static IServiceCollection AddModule(this IServiceCollection services, IModule module, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(config);

        var moduleType = module.GetType();

        // Prevent duplicate registration of the same module type within one service collection
        if (IsModuleRegistered(services, moduleType))
        {
            return services;
        }

        services.AddSingleton<IModule>(module);
        services.AddSingleton(moduleType, module);

        module.AddServices(services, config);

        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var modules = app.ServiceProvider.GetServices<IModule>();
        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }

    private static bool IsModuleRegistered(IServiceCollection services, Type moduleType)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == moduleType)
            {
                return true;
            }

            if (descriptor.ServiceType == typeof(IModule))
            {
                if (descriptor.IsKeyedService)
                {
                    if (descriptor.KeyedImplementationType == moduleType ||
                        descriptor.KeyedImplementationInstance?.GetType() == moduleType ||
                        (descriptor.KeyedImplementationFactory != null &&
                         descriptor.KeyedImplementationFactory.Method.ReturnType == moduleType))
                    {
                        return true;
                    }
                }
                else
                {
                    if (descriptor.ImplementationType == moduleType ||
                        descriptor.ImplementationInstance?.GetType() == moduleType ||
                        (descriptor.ImplementationFactory != null &&
                         descriptor.ImplementationFactory.Method.ReturnType == moduleType))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
