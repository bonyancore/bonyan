using Construction.Procurement.Core.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("supplier_invoices", table => table.HasCheckConstraint(
            "ck_supplier_invoices_status", "status in ('draft','matched','disputed','paymentscheduled','paid')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.SupplierId, item.InvoiceNumber }).IsUnique();
        builder.Property(item => item.InvoiceNumber).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Status).ConfigureStatus();
        builder.Property(item => item.DisputeReason).HasMaxLength(2000);
        builder.Property(item => item.SubmittedAt).HasColumnType("timestamp with time zone");
        builder.Ignore(item => item.TotalAmount);
        builder.Ignore(item => item.Currency);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseOrders.PurchaseOrder>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Construction.Procurement.Core.Domain.Suppliers.Supplier>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.SupplierId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(item => new { item.TenantId, item.SupplierInvoiceId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable("supplier_invoice_lines", table =>
        {
            table.HasCheckConstraint("ck_supplier_invoice_lines_quantity", "quantity_amount > 0");
            table.HasCheckConstraint("ck_supplier_invoice_lines_unit_price", "unit_price_amount >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.SupplierInvoiceId, item.PurchaseOrderLineId }).IsUnique();
        builder.Property(item => item.QuantityAmount).HasField("_quantityAmount").HasColumnName("quantity_amount").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.QuantityUnit).HasField("_quantityUnit").HasColumnName("quantity_unit").HasMaxLength(20).IsRequired();
        builder.Property(item => item.UnitPriceAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Ignore(item => item.Quantity);
        builder.Ignore(item => item.Total);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseOrders.PurchaseOrderLine>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseOrderLineId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
