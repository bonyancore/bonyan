using Construction.Procurement.Core.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers", table => table.HasCheckConstraint(
            "ck_suppliers_status", "status in ('active', 'inactive')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.CorePartyId }).IsUnique();
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CorePartyId).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Email).HasMaxLength(320);
        builder.Property(item => item.Phone).HasMaxLength(40);
        builder.Property(item => item.Status).ConfigureStatus();
        builder.ConfigureAudit();
    }
}
