using Construction.Procurement.Core.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.ReceivedAt });
        builder.Property(item => item.ReceivedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.ReversedAt).HasColumnType("timestamp with time zone");
        builder.Property(item => item.ReversalReason).HasMaxLength(1000);
        builder.Ignore(item => item.IsReversed);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseOrders.PurchaseOrder>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(item => new { item.TenantId, item.GoodsReceiptId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("goods_receipt_lines", table => table.HasCheckConstraint(
            "ck_goods_receipt_lines_quantity", "quantity_amount > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.GoodsReceiptId, item.PurchaseOrderLineId }).IsUnique();
        builder.Property(item => item.QuantityAmount).HasField("_quantityAmount").HasColumnName("quantity_amount").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.QuantityUnit).HasField("_quantityUnit").HasColumnName("quantity_unit").HasMaxLength(20).IsRequired();
        builder.Ignore(item => item.Quantity);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseOrders.PurchaseOrderLine>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseOrderLineId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
