using Construction.Procurement.Core.Domain.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", table => table.HasCheckConstraint(
            "ck_purchase_orders_status", "status in ('draft','pendingapproval','approved','rejected','issued','cancelled')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.Number, item.Revision }).IsUnique();
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.ProjectId).IsRequired();
        builder.Property(item => item.SupplierId).IsRequired();
        builder.Property(item => item.PurchaseRequestId).IsRequired();
        builder.Property(item => item.Number).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Revision).IsRequired();
        builder.Property(item => item.BudgetReservedAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Status).ConfigureStatus();
        builder.Property(item => item.ApprovalReference).HasMaxLength(200);
        builder.Property(item => item.RejectionReason).HasMaxLength(1000);
        builder.Property(item => item.CancellationReason).HasMaxLength(1000);
        builder.Property(item => item.IssuedAt).HasColumnType("timestamp with time zone");
        builder.Ignore(item => item.TotalAmount);
        builder.Ignore(item => item.Currency);
        builder.ConfigureAudit();
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => new { line.TenantId, line.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Revisions).WithOne().HasForeignKey(revision => new { revision.TenantId, revision.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseRequests.PurchaseRequest>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Construction.Procurement.Core.Domain.Suppliers.Supplier>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.SupplierId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", table =>
        {
            table.HasCheckConstraint("ck_purchase_order_lines_quantity", "quantity_amount > 0");
            table.HasCheckConstraint("ck_purchase_order_lines_unit_price", "unit_price_amount >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Description).HasMaxLength(500).IsRequired();
        builder.Property(item => item.QuantityAmount).HasField("_quantityAmount").HasColumnName("quantity_amount").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.QuantityUnit).HasField("_quantityUnit").HasColumnName("quantity_unit").HasMaxLength(20).IsRequired();
        builder.Property(item => item.UnitPriceAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Ignore(item => item.Quantity);
        builder.Ignore(item => item.UnitPrice);
        builder.Ignore(item => item.LineTotal);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseRequests.PurchaseRequestItem>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseRequestItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
