using Construction.Kernel.Tenancy;
using Construction.Persistence;
using Construction.Platform.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Construction.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options,
    ITenantContext tenantContext)
    : ModuleDbContext(options, Schema, tenantContext)
{
    public const string Schema = "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
