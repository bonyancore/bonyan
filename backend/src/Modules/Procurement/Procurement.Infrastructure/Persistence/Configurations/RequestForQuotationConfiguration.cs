using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> builder)
    {
        builder.ToTable("requests_for_quotation", table => table.HasCheckConstraint(
            "ck_requests_for_quotation_status", "status in ('draft','issued','awarded','closed','cancelled')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.PurchaseRequestId });
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.PurchaseRequestId).IsRequired();
        builder.Property(item => item.Status).ConfigureStatus();
        builder.ConfigureAudit();
        builder.HasMany<RequestForQuotationInvitation>("_invitations").WithOne().HasForeignKey(item => new { item.TenantId, item.RequestForQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Construction.Procurement.Core.Domain.PurchaseRequests.PurchaseRequest>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation("_invitations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(item => item.Quotes).WithOne().HasForeignKey(item => new { item.TenantId, item.RequestForQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Quotes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RequestForQuotationInvitationConfiguration : IEntityTypeConfiguration<RequestForQuotationInvitation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationInvitation> builder)
    {
        builder.ToTable("rfq_invitations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.RequestForQuotationId, item.SupplierId }).IsUnique();
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.Suppliers.Supplier>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.SupplierId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SupplierQuoteConfiguration : IEntityTypeConfiguration<SupplierQuote>
{
    public void Configure(EntityTypeBuilder<SupplierQuote> builder)
    {
        builder.ToTable("supplier_quotes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.RequestForQuotationId, item.SupplierId }).IsUnique();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(item => item.SubmittedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Ignore(item => item.TotalAmount);
        builder.ConfigureAudit();
        builder.HasOne<Construction.Procurement.Core.Domain.Suppliers.Supplier>()
            .WithMany().HasForeignKey(item => new { item.TenantId, item.SupplierId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(item => new { item.TenantId, item.SupplierQuoteId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SupplierQuoteLineConfiguration : IEntityTypeConfiguration<SupplierQuoteLine>
{
    public void Configure(EntityTypeBuilder<SupplierQuoteLine> builder)
    {
        builder.ToTable("supplier_quote_lines", table =>
        {
            table.HasCheckConstraint("ck_supplier_quote_lines_quantity", "quantity_amount > 0");
            table.HasCheckConstraint("ck_supplier_quote_lines_unit_price", "unit_price_amount >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.SupplierQuoteId, item.PurchaseRequestItemId }).IsUnique();
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
