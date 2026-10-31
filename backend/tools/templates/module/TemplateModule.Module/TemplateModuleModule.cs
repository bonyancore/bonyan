using Construction.Modules;
using Construction.TemplateModule.Api;
using Construction.TemplateModule.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.TemplateModule;

public sealed class TemplateModuleModule : IModule
{
    public string Code => "templatemodule";

    public IReadOnlyCollection<PermissionDefinition> Permissions => [];

    public IReadOnlyCollection<MigrationInfo> Migrations => [];

    public void AddServices(IServiceCollection services, IConfiguration config)
    {
        services.AddTemplateModuleInfrastructure(config);
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapTemplateModuleEndpoints();
    }
}

