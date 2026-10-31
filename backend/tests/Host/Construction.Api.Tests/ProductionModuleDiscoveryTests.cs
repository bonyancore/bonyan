using Construction.Api;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Modules;
using Construction.Persistence;
using Construction.Platform;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Construction.Api.Tests;

public sealed record ProductionDiscoveryResult(
    IReadOnlyCollection<string> DiscoveredModuleCodes,
    IReadOnlyCollection<Type> DiscoveredModuleTypes,
    IReadOnlyCollection<Type> DeclaredDbContextTypes,
    IReadOnlyCollection<Type> ResolvedDbContextTypes,
    IReadOnlyCollection<(string Schema, string Table)> DiscoveredTenantScopedTables);

public class ProductionModuleDiscoveryTests
{
    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; }
        public TenantId Current { get; set; }
    }

    public static ProductionDiscoveryResult DiscoverProductionTenantScopedTables(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddAppModules(config);
        services.AddScoped<ITenantContext, FakeTenantContext>();

        using var rootProvider = services.BuildServiceProvider();
        using var scope = rootProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var modules = sp.GetServices<IModule>().ToList();
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("Architecture verification failure: No IModule instances discovered via AddAppModules.");
        }

        var resolvedContextTypes = new List<Type>();
        var discoveredTables = new HashSet<(string Schema, string Table)>();

        foreach (var module in modules)
        {
            foreach (var migration in module.Migrations)
            {
                var dbContextType = migration.DbContextType;

                if (!typeof(ModuleDbContext).IsAssignableFrom(dbContextType))
                {
                    throw new InvalidOperationException(
                        $"Module '{module.Code}' declares Migration DbContext '{dbContextType.FullName}' which does not inherit from ModuleDbContext.");
                }

                var dbContext = (DbContext)sp.GetRequiredService(dbContextType);
                resolvedContextTypes.Add(dbContextType);

                var model = dbContext.Model;
                foreach (var entityType in model.GetEntityTypes())
                {
                    if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
                    {
                        var tableMappings = entityType.GetTableMappings();
                        foreach (var mapping in tableMappings)
                        {
                            var table = mapping.Table;
                            var schema = table.Schema ?? migration.Schema;
                            var tableName = table.Name;

                            if (!string.IsNullOrWhiteSpace(tableName))
                            {
                                discoveredTables.Add((schema, tableName));
                            }
                        }
                    }
                }
            }
        }

        return new ProductionDiscoveryResult(
            modules.Select(m => m.Code).ToList(),
            modules.Select(m => m.GetType()).ToList(),
            modules.SelectMany(m => m.Migrations.Select(mig => mig.DbContextType)).ToList(),
            resolvedContextTypes,
            discoveredTables);
    }

    [Fact]
    public void ProductionDiscovery_DiscoversPlatformModuleAndVerifiesBaselineExemptions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=construction;Username=app;Password=app_dev_pw"
            })
            .Build();

        var result = DiscoverProductionTenantScopedTables(config);

        // 1. Explicitly assert PlatformModule is discovered
        Assert.Contains("platform", result.DiscoveredModuleCodes);
        Assert.Contains(typeof(PlatformModule), result.DiscoveredModuleTypes);

        // 2. Explicitly assert PlatformDbContext is declared in migration metadata and successfully resolved from DI
        Assert.Contains(typeof(PlatformDbContext), result.DeclaredDbContextTypes);
        Assert.Contains(typeof(PlatformDbContext), result.ResolvedDbContextTypes);

        // 3. Explicitly assert the production baseline contains 0 tenant-scoped tables
        // (Platform.Core.Domain.Tenancy.Tenant is the root company aggregate and is exempt)
        Assert.Empty(result.DiscoveredTenantScopedTables);
    }
}

