using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;

namespace Construction.Persistence;

/// <summary>Base for every module DbContext: sets the module's schema, conventions, and global filters.</summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema, ITenantContext tenantContext)
    : DbContext(options)
{
    protected ITenantContext TenantContext { get; } = tenantContext;

    /// <summary>
    /// Evaluated dynamically per query execution by EF Core's query compiler.
    /// Returns the active tenant struct when present and valid; otherwise null when unauthenticated.
    /// Throws InvalidOperationException if an active tenant is present but invalid (e.g. default/empty).
    /// </summary>
    public TenantId? CurrentTenantFilterId
    {
        get
        {
            if (!TenantContext.HasTenant)
            {
                return null;
            }

            var current = TenantContext.Current;
            current.EnsureValid();
            return current;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<TenantId>()
            .HaveConversion<TenantIdValueConverter>();

        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);

        // xmin optimistic concurrency token convention
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IConcurrencyVersioned).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<uint>(nameof(IConcurrencyVersioned.RowVersion))
                    .IsRowVersion();
            }
        }

        // Apply composed tenant & soft-delete query filters
        modelBuilder.ApplyGlobalQueryFilters(this);

        base.OnModelCreating(modelBuilder);
    }
}
