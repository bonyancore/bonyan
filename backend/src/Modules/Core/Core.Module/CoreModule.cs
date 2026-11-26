using Construction.Modules;
using Construction.Core.Api;
using Construction.Core.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Core;

public sealed class CoreModule : IModule
{
    public string Code => "core";

    public IReadOnlyCollection<PermissionDefinition> Permissions => [];

    public IReadOnlyCollection<MigrationInfo> Migrations => [];

    public void AddServices(IServiceCollection services, IConfiguration config)
    {
        services.AddCoreInfrastructure(config);
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapCoreEndpoints();
    }
}

