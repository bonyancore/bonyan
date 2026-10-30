using Construction.Platform.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants", t =>
        {
            t.HasCheckConstraint("ck_tenants_name", "name_ar is not null or name_en is not null");
            t.HasCheckConstraint("ck_tenants_status",
                "status in ('provisioning','active','trial','suspended','pending_deletion','deleted')");
            t.HasCheckConstraint("ck_tenants_plan", "plan_code in ('starter','growth','enterprise')");
            t.HasCheckConstraint("ck_tenants_code", "code ~ '^[a-z0-9-]{3,40}$'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PlanCode);
        b.Property(x => x.BaseCurrency).HasColumnType("char(3)");
        b.Property(x => x.CountryCode).HasColumnType("char(2)");
    }
}
