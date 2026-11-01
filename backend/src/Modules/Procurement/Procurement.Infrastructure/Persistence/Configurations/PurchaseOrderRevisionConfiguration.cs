using Construction.Procurement.Core.Domain.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderRevisionConfiguration : IEntityTypeConfiguration<PurchaseOrderRevision>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderRevision> builder)
    {
        builder.ToTable("purchase_order_revisions", table =>
        {
            table.HasCheckConstraint("ck_purchase_order_revisions_revision", "revision > 0");
            table.HasCheckConstraint("ck_purchase_order_revisions_total", "total_amount >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.Revision }).IsUnique();
        builder.Property(item => item.Revision).IsRequired();
        builder.Property(item => item.SupplierId).IsRequired();
        builder.Property(item => item.ProjectId).IsRequired();
        builder.Property(item => item.TotalAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(item => item.ApprovalReference).HasMaxLength(200).IsRequired();
        builder.Property(item => item.IssuedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.ConfigureAudit();
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => new { line.TenantId, line.PurchaseOrderRevisionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseOrderRevisionLineConfiguration : IEntityTypeConfiguration<PurchaseOrderRevisionLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderRevisionLine> builder)
    {
        builder.ToTable("purchase_order_revision_lines", table =>
        {
            table.HasCheckConstraint("ck_purchase_order_revision_lines_quantity", "quantity_amount > 0");
            table.HasCheckConstraint("ck_purchase_order_revision_lines_price", "unit_price_amount >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Description).HasMaxLength(500).IsRequired();
        builder.Property(item => item.QuantityAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.QuantityUnit).HasMaxLength(20).IsRequired();
        builder.Property(item => item.UnitPriceAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.ConfigureAudit();
    }
}
