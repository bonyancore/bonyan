using Construction.Modules;
using Construction.Platform.Api;
using Construction.Platform.Infrastructure;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform;

public sealed class PlatformModule : IModule
{
    public string Code => "platform";

    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("platform.tenant.read", "View tenant details"),
        new("platform.tenant.update", "Update tenant settings")
    ];

    public IReadOnlyCollection<MigrationInfo> Migrations =>
    [
        new(PlatformDbContext.Schema, typeof(PlatformDbContext))
    ];

    public void AddServices(IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPlatformEndpoints();
    }
}

