using Construction.Docs.Core.Domain.Documents;
using Construction.Kernel.Tenancy;
using Construction.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construction.Docs.Infrastructure.Persistence;

public sealed class DocsDbContext(DbContextOptions<DocsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, Schema, tenantContext)
{
    public const string Schema = "docs";
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocsDbContext).Assembly);
    }
}
