using Construction.Persistence;
using Construction.Platform.Core.Domain.Messaging;
using Construction.Platform.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Construction.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
