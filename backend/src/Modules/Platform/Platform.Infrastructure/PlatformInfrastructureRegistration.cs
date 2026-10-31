
using System.Data.Common;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Jobs.Outbox;
using Construction.Platform.Infrastructure.Jobs.Runner;
using Construction.Platform.Core.Application.Outbox;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Construction.Platform.Infrastructure;

public static class PlatformInfrastructureRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<TenantSessionInterceptor>();
        services.AddScoped<IDeadLetterStore, Construction.Platform.Infrastructure.Application.DeadLetterService>();

        services.AddDbContext<PlatformDbContext>((sp, o) => o
            .UseNpgsql(
                config.GetConnectionString("Default"),
                n => n.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    PlatformDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<TenantSessionInterceptor>()));

        // Transactional outbox writer shares the module DbContext / caller transaction.
        services.AddScoped<IOutbox>(sp => new EfOutboxWriter(
            sp.GetRequiredService<PlatformDbContext>(),
            sp.GetRequiredService<ITenantContext>()));

        // PostgreSQL store for processed events (PLT-201).
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured.");

        services.AddSingleton<IProcessedEventStore>(_ =>
            new DbProcessedEventStore(
                cancellationToken =>
                    Task.FromResult<DbConnection>(
                        new NpgsqlConnection(connectionString))));

        return services;
    }

    /// <summary>
    /// Registers Platform jobs and the hand-written job runner (PLT-203).
    /// Only call from Worker when Modules:Platform is enabled.
    /// </summary>
    public static IServiceCollection AddPlatformWorkerJobs(
        this IServiceCollection services, IConfiguration config)
    {
        var moduleOptions = BindModuleOptions(config);
        services.AddSingleton(moduleOptions);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITenantJobEnablement>(
            _ => new DictionaryTenantJobEnablement());
        services.AddSingleton<IJobRunStore, InMemoryJobRunStore>();
        services.AddSingleton<IJobLock, InMemoryJobLock>();
        services.AddSingleton<IJob, PlatformHeartbeatJob>();
        services.AddSingleton(sp => new JobCatalog(
            sp.GetServices<IJob>(),
            sp.GetRequiredService<ModuleJobOptions>()));
        services.AddSingleton<JobRunner>();

        return services;
    }

    public static ModuleJobOptions BindModuleOptions(IConfiguration config)
    {
        static bool Flag(IConfiguration c, string key, bool defaultValue)
        {
            var raw = c[key];
            return bool.TryParse(raw, out var v) ? v : defaultValue;
        }

        return new ModuleJobOptions
        {
            Platform = Flag(config, "Modules:Platform", true),
            Cost = Flag(config, "Modules:Cost", false),
            Cash = Flag(config, "Modules:Cash", false)
        };
    }
}
