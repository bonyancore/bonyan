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
        new("platform.tenant.update", "Update tenant settings"),
        new("platform.outbox.view", "View dead-lettered outbox messages"),
        new("platform.outbox.manage", "Replay or discard dead-lettered outbox messages"),
        new("platform.outbox.operator", "Replay or discard financial dead letters"),
        new("platform.file.upload", "Create upload sessions and complete uploads"),
        new("platform.file.view", "View and download files"),
        new("platform.file.link", "Link files to business records"),
        new("platform.file.delete", "Delete files")
    ];

    public IReadOnlyCollection<MigrationInfo> Migrations =>
    [
        new(PlatformDbContext.Schema, typeof(PlatformDbContext))
    ];

    public void AddServices(IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        services.AddPlatformApi();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPlatformEndpoints();
    }
}
