using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Persistence.Tests;

public class GlobalQueryFilterTests
{
    public class SampleTenantEntity : ITenantScopedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; set; }
    }

    public class SampleSoftDeleteEntity : ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    public class SampleComposedEntity : ITenantScopedEntity, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class SampleCatalogEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }

    public abstract class HierarchyBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public class IllegalDerivedTenantEntity : HierarchyBase, ITenantScopedEntity
    {
        public TenantId TenantId { get; set; }
    }

    public class IllegalDerivedSoftDeleteEntity : HierarchyBase, ISoftDeletable
    {
        public bool IsDeleted { get; set; }
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; }
        public TenantId Current { get; set; }
    }

    public class FilterTestDbContext(
        DbContextOptions<FilterTestDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, "test", tenantContext)
    {
        public DbSet<SampleTenantEntity> TenantEntities => Set<SampleTenantEntity>();
        public DbSet<SampleSoftDeleteEntity> SoftDeleteEntities => Set<SampleSoftDeleteEntity>();
        public DbSet<SampleComposedEntity> ComposedEntities => Set<SampleComposedEntity>();
        public DbSet<SampleCatalogEntity> CatalogEntities => Set<SampleCatalogEntity>();
    }

    private static (FilterTestDbContext Context, FakeTenantContext Tenant) CreateDbContext(string dbName)
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new FilterTestDbContext(options, tenant);
        return (context, tenant);
    }

    [Fact]
    public void Query_TenantScoped_OnlyReturnsRowsForCurrentTenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.TenantEntities.AddRange(
                new SampleTenantEntity { Name = "A1", TenantId = tenantA },
                new SampleTenantEntity { Name = "A2", TenantId = tenantA },
                new SampleTenantEntity { Name = "B1", TenantId = tenantB });
            seedCtx.SaveChanges();
        }

        var (queryCtx, tenantCtx) = CreateDbContext(dbName);
        tenantCtx.HasTenant = true;
        tenantCtx.Current = tenantA;

        var results = queryCtx.TenantEntities.ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(tenantA, r.TenantId));
    }

    [Fact]
    public void Query_TenantScoped_EvaluatesTenantDynamicallyPerContext()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.TenantEntities.AddRange(
                new SampleTenantEntity { Name = "A1", TenantId = tenantA },
                new SampleTenantEntity { Name = "B1", TenantId = tenantB });
            seedCtx.SaveChanges();
        }

        // Query under Tenant A
        var (ctxA, tCtxA) = CreateDbContext(dbName);
        tCtxA.HasTenant = true;
        tCtxA.Current = tenantA;
        var resultsA = ctxA.TenantEntities.ToList();
        Assert.Single(resultsA);
        Assert.Equal("A1", resultsA[0].Name);

        // Query under Tenant B
        var (ctxB, tCtxB) = CreateDbContext(dbName);
        tCtxB.HasTenant = true;
        tCtxB.Current = tenantB;
        var resultsB = ctxB.TenantEntities.ToList();
        Assert.Single(resultsB);
        Assert.Equal("B1", resultsB[0].Name);
    }

    [Fact]
    public void Query_SameDbContextType_CachedModel_EvaluatesCurrentTenantPerInstance()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.TenantEntities.AddRange(
                new SampleTenantEntity { Name = "A1", TenantId = tenantA },
                new SampleTenantEntity { Name = "B1", TenantId = tenantB });
            seedCtx.SaveChanges();
        }

        // Reusing options (which caches IModel) across two context instances
        var options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tA = new FakeTenantContext { HasTenant = true, Current = tenantA };
        using (var ctx1 = new FilterTestDbContext(options, tA))
        {
            var res = ctx1.TenantEntities.ToList();
            Assert.Single(res);
            Assert.Equal(tenantA, res[0].TenantId);
        }

        var tB = new FakeTenantContext { HasTenant = true, Current = tenantB };
        using (var ctx2 = new FilterTestDbContext(options, tB))
        {
            var res = ctx2.TenantEntities.ToList();
            Assert.Single(res);
            Assert.Equal(tenantB, res[0].TenantId);
        }
    }

    [Fact]
    public void Query_TenantScoped_WithoutActiveTenant_ReturnsZeroRows_WithoutConverterException()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.TenantEntities.Add(new SampleTenantEntity { Name = "A1", TenantId = TenantId.New() });
            seedCtx.SaveChanges();
        }

        var (queryCtx, tenantCtx) = CreateDbContext(dbName);
        tenantCtx.HasTenant = false;

        var results = queryCtx.TenantEntities.ToList();
        Assert.Empty(results);
    }

    [Fact]
    public void Query_TenantScoped_WithInvalidActiveTenant_ThrowsInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        var (queryCtx, tenantCtx) = CreateDbContext(dbName);
        tenantCtx.HasTenant = true;
        tenantCtx.Current = default(TenantId);

        var ex = Assert.Throws<InvalidOperationException>(() => queryCtx.TenantEntities.ToList());
        Assert.Contains("TenantId is not initialized", ex.Message);
    }

    [Fact]
    public void Query_NonTenantEntity_UnfilteredByTenant()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.CatalogEntities.Add(new SampleCatalogEntity { Name = "Catalog Item" });
            seedCtx.SaveChanges();
        }

        var (queryCtx, tenantCtx) = CreateDbContext(dbName);
        tenantCtx.HasTenant = false; // No tenant active

        var results = queryCtx.CatalogEntities.ToList();
        Assert.Single(results);
    }

    [Fact]
    public void Query_ExcludesSoftDeletedEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.SoftDeleteEntities.AddRange(
                new SampleSoftDeleteEntity { Name = "Active", IsDeleted = false },
                new SampleSoftDeleteEntity { Name = "Deleted", IsDeleted = true });
            seedCtx.SaveChanges();
        }

        var (queryCtx, _) = CreateDbContext(dbName);
        var results = queryCtx.SoftDeleteEntities.ToList();
        Assert.Single(results);
        Assert.Equal("Active", results[0].Name);
    }

    [Fact]
    public void Query_WithIgnoreQueryFilters_IncludesAllTenantsAndDeleted()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        using (var seedCtx = CreateDbContext(dbName).Context)
        {
            seedCtx.ComposedEntities.AddRange(
                new SampleComposedEntity { Name = "A-Active", TenantId = tenantA, IsDeleted = false },
                new SampleComposedEntity { Name = "A-Deleted", TenantId = tenantA, IsDeleted = true },
                new SampleComposedEntity { Name = "B-Active", TenantId = tenantB, IsDeleted = false });
            seedCtx.SaveChanges();
        }

        var (queryCtx, tenantCtx) = CreateDbContext(dbName);
        tenantCtx.HasTenant = true;
        tenantCtx.Current = tenantA;

        // Default query only returns A-Active
        var defaultRes = queryCtx.ComposedEntities.ToList();
        Assert.Single(defaultRes);
        Assert.Equal("A-Active", defaultRes[0].Name);

        // IgnoreQueryFilters returns all 3 entities
        var ignoredRes = queryCtx.ComposedEntities.IgnoreQueryFilters().ToList();
        Assert.Equal(3, ignoredRes.Count);
    }

    [Fact]
    public void DerivedOnly_ITenantScoped_ThrowsAtModelCreating()
    {
        var options = new DbContextOptionsBuilder<IllegalTenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var ctx = new IllegalTenantDbContext(options, new FakeTenantContext());
            _ = ctx.Model;
        });

        Assert.Contains("ITenantScopedEntity must be declared on the root", ex.Message);
    }

    private class IllegalTenantDbContext(
        DbContextOptions<IllegalTenantDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, "illegal", tenantContext)
    {
        public DbSet<HierarchyBase> BaseEntities => Set<HierarchyBase>();
        public DbSet<IllegalDerivedTenantEntity> Entities => Set<IllegalDerivedTenantEntity>();
    }
}
