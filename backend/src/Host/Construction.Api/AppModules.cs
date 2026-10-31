using Construction.Platform.Infrastructure;

namespace Construction.Api;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        // services.AddPlatformApi();   // when Platform.Api exposes its registration
        return services;
    }

    public static IEndpointRouteBuilder MapAppModules(this IEndpointRouteBuilder app)
    {
        // app.MapPlatformEndpoints();  // when Platform.Api defines endpoints
        return app;
    }
}
