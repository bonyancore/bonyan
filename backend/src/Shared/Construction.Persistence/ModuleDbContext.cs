using Microsoft.EntityFrameworkCore;

namespace Construction.Persistence;

/// <summary>Base for every module DbContext: sets the module's schema.</summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        base.OnModelCreating(modelBuilder);
    }
}
