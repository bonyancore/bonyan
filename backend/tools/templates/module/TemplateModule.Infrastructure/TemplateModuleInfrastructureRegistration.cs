using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.TemplateModule.Infrastructure;

public static class TemplateModuleInfrastructureRegistration
{
    public static IServiceCollection AddTemplateModuleInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        return services;
    }
}

