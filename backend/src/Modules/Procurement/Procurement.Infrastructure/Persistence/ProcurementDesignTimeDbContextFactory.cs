using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Construction.Procurement.Infrastructure.Persistence;

public sealed class ProcurementDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProcurementDbContext>
{
    public ProcurementDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=construction;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", ProcurementDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ProcurementDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public bool HasTenant => false;

        public TenantId Current => throw new InvalidOperationException("No tenant is set at design time.");
    }
}
