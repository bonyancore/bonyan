using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Core.Infrastructure;

public static class CoreInfrastructureRegistration
{
    public static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        return services;
    }
}

