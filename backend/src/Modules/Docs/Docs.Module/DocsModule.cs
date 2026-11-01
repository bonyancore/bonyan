using Construction.Docs.Api;
using Construction.Docs.Infrastructure;
using Construction.Docs.Infrastructure.Persistence;
using Construction.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Docs;

public sealed class DocsModule : IModule
{
    public string Code => "docs";
    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("docs.document.read", "View linked document metadata and versions"),
        new("docs.document.manage", "Link documents and add versions"),
        new("docs.document.approve", "Approve a document version"),
    ];
    public IReadOnlyCollection<MigrationInfo> Migrations =>
    [
        new(DocsDbContext.Schema, typeof(DocsDbContext)),
    ];

    public void AddServices(IServiceCollection services, IConfiguration config) => services.AddDocsInfrastructure(config);

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapDocsEndpoints();
}
