
using System.Data.Common;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Application;
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

        // One implementation backs both the dead-letter admin endpoints and the lag metric, so the
        // tenant-scoped RLS transaction is written once.
        services.AddScoped<DeadLetterService>();
        services.AddScoped<IDeadLetterStore>(sp => sp.GetRequiredService<DeadLetterService>());
        services.AddScoped<IOutboxMetricsStore>(sp => sp.GetRequiredService<DeadLetterService>());

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
        this IServiceCollection services, IConfiguration config, bool allowInMemoryStores = false)
    {
        var moduleOptions = BindModuleOptions(config);
        services.AddSingleton(moduleOptions);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITenantJobEnablement>(
            _ => new DictionaryTenantJobEnablement());
        services.AddSingleton<IJob, PlatformHeartbeatJob>();

        // Production default is PostgreSQL. The in-memory adapters exist for tests and for an
        // explicitly-enabled development run, and are refused outside Development/Test (§8.5).
        var useInMemory = UseInMemoryAdapter(config, "Platform:Jobs:Store", allowInMemoryStores);
        services.AddSingleton<IJobRunStore>(_ => useInMemory
            ? new InMemoryJobRunStore()
            : new PostgresJobRunStore(JobConnectionFactory(config)));
        services.AddSingleton<IJobLock>(_ => useInMemory
            ? new InMemoryJobLock()
            : new PostgresJobLock(JobConnectionFactory(config)));

        services.AddSingleton(sp => new JobCatalog(
            sp.GetServices<IJob>(),
            sp.GetRequiredService<ModuleJobOptions>()));
        services.AddSingleton<JobRunner>();

        return services;
    }

    /// <summary>
    /// Chooses a test adapter only when the configuration explicitly asks for it; refusing in
    /// production stops an in-memory store from silently replacing database persistence.
    /// </summary>
    private static bool UseInMemoryAdapter(IConfiguration config, string key, bool allowInMemory)
    {
        if (!string.Equals(config[key], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!allowInMemory)
        {
            throw new InvalidOperationException(
                $"'{key}=InMemory' is only allowed in Development or Test; refusing in this environment.");
        }

        return true;
    }

    private static Func<CancellationToken, Task<DbConnection>> JobConnectionFactory(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        return _ => Task.FromResult<DbConnection>(new NpgsqlConnection(connectionString));
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
