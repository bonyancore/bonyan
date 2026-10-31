using Construction.Platform.Infrastructure;
using Construction.Platform.Api;

namespace Construction.Api;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        services.AddPlatformApi();
        return services;
    }

    public static IEndpointRouteBuilder MapAppModules(this IEndpointRouteBuilder app)
    {
        app.MapPlatformEndpoints();
        return app;
    }
}
