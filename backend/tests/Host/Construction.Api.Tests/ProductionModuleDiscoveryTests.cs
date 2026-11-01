using Construction.Api;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Modules;
using Construction.Persistence;
using Construction.Platform;
using Construction.Platform.Infrastructure.Persistence;
using Construction.Procurement;
using Construction.Procurement.Infrastructure.Persistence;
using Construction.Docs;
using Construction.Docs.Infrastructure.Persistence;
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
    public void ProductionDiscovery_DiscoversPlatformProcurementAndDocsModules()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=construction;Username=app;Password=app_dev_pw"
            })
            .Build();

        var result = DiscoverProductionTenantScopedTables(config);

        // Each module contributes its entry point, migrations, and tenant-scoped tables.
        Assert.Contains("platform", result.DiscoveredModuleCodes);
        Assert.Contains(typeof(PlatformModule), result.DiscoveredModuleTypes);
        Assert.Contains("procurement", result.DiscoveredModuleCodes);
        Assert.Contains(typeof(ProcurementModule), result.DiscoveredModuleTypes);
        Assert.Contains("docs", result.DiscoveredModuleCodes);
        Assert.Contains(typeof(DocsModule), result.DiscoveredModuleTypes);

        Assert.Contains(typeof(PlatformDbContext), result.DeclaredDbContextTypes);
        Assert.Contains(typeof(PlatformDbContext), result.ResolvedDbContextTypes);
        Assert.Contains(typeof(ProcurementDbContext), result.DeclaredDbContextTypes);
        Assert.Contains(typeof(ProcurementDbContext), result.ResolvedDbContextTypes);
        Assert.Contains(typeof(DocsDbContext), result.DeclaredDbContextTypes);
        Assert.Contains(typeof(DocsDbContext), result.ResolvedDbContextTypes);
        Assert.Contains(("procurement", "purchase_requests"), result.DiscoveredTenantScopedTables);
        Assert.Contains(("procurement", "purchase_orders"), result.DiscoveredTenantScopedTables);
        Assert.Contains(("docs", "documents"), result.DiscoveredTenantScopedTables);
        Assert.Contains(("docs", "document_versions"), result.DiscoveredTenantScopedTables);
    }
}

