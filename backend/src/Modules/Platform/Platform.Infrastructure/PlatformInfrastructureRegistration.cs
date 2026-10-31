using Construction.Kernel.Users;
using Construction.Persistence;
using Construction.Persistence.Interceptors;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Construction.Platform.Infrastructure;

public static class PlatformInfrastructureRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddPersistenceInfrastructure();
        services.TryAddScoped<IUserContext, NullUserContext>();

        services.AddDbContext<PlatformDbContext>((sp, o) => o
            .UseNpgsql(
                config.GetConnectionString("Default"),
                n => n.MigrationsHistoryTable("__ef_migrations_history", PlatformDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
