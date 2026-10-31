using Construction.Persistence;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Infrastructure;

public static class PlatformInfrastructureRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<TenantSessionInterceptor>();

        services.AddDbContext<PlatformDbContext>((sp, o) => o
            .UseNpgsql(
                config.GetConnectionString("Default"),
                n => n.MigrationsHistoryTable("__ef_migrations_history", PlatformDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
