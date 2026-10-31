using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Persistence.Tests;

public class ConcurrencyMappingTests
{
    private class TestVersionedEntity : IConcurrencyVersioned
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public uint RowVersion { get; set; }
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant => false;
        public TenantId Current => default;
    }

    private class VersionedTestDbContext(
        DbContextOptions<VersionedTestDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, "public", tenantContext)
    {
        public DbSet<TestVersionedEntity> VersionedEntities => Set<TestVersionedEntity>();
    }

    [Fact]
    public void RowVersion_Property_MappedAsRowVersion_InMetadata()
    {
        var options = new DbContextOptionsBuilder<VersionedTestDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var ctx = new VersionedTestDbContext(options, new FakeTenantContext());
        var entityType = ctx.Model.FindEntityType(typeof(TestVersionedEntity))!;
        var property = entityType.FindProperty(nameof(IConcurrencyVersioned.RowVersion))!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void RowVersion_HasNoColumnNameOverride()
    {
        var options = new DbContextOptionsBuilder<VersionedTestDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var ctx = new VersionedTestDbContext(options, new FakeTenantContext());
        var entityType = ctx.Model.FindEntityType(typeof(TestVersionedEntity))!;
        var property = entityType.FindProperty(nameof(IConcurrencyVersioned.RowVersion))!;

        // Confirms SetColumnName("xmin") was NOT called explicitly
        // The default column name before finalizing convention is the property name ("RowVersion")
        Assert.Null(property.GetColumnName(Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table("test_versioned_entities", "public")));
    }
}

