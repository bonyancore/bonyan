using Construction.Modules;
using Construction.Platform;
using Construction.Platform.Api;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Api;

public static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddModule<PlatformModule>(config);
        services.AddPlatformApi();
        return services;
    }

    public static IEndpointRouteBuilder MapAppModules(this IEndpointRouteBuilder app)
    {
        app.MapModuleEndpoints();
        return app;
    }
}
