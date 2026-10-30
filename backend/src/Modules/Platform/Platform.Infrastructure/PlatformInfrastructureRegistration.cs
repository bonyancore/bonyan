using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Jobs.Outbox;
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

        // Transactional outbox writer shares the module DbContext / caller transaction.
        services.AddScoped<IOutbox>(sp => new EfOutboxWriter(
            sp.GetRequiredService<PlatformDbContext>(),
            sp.GetRequiredService<ITenantContext>()));

        return services;
    }
}
