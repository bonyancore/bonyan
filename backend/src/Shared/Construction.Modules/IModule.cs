using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Modules;

public interface IModule
{
    string Code { get; }
    void AddServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder app);
    IReadOnlyCollection<PermissionDefinition> Permissions { get; }
    IReadOnlyCollection<MigrationInfo> Migrations { get; }
}

