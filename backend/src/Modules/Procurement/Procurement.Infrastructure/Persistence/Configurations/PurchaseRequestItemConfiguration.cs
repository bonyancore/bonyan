using Construction.Procurement.Core.Domain.PurchaseRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseRequestItemConfiguration : IEntityTypeConfiguration<PurchaseRequestItem>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestItem> builder)
    {
        builder.ToTable("purchase_request_items", table =>
        {
            table.HasCheckConstraint("ck_purchase_request_items_quantity_amount", "quantity_amount > 0");
            table.HasCheckConstraint(
                "ck_purchase_request_items_quantity_unit",
                "length(trim(quantity_unit)) between 1 and 20");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.PurchaseRequestId).IsRequired();
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(500).IsRequired();
        builder.Ignore(item => item.Quantity);

        builder.Property(item => item.QuantityAmount)
            .HasField("_quantityAmount")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("quantity_amount")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(item => item.QuantityUnit)
            .HasField("_quantityUnit")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("quantity_unit")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnType("uuid");
        builder.Property(item => item.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.UpdatedBy).HasColumnType("uuid");
    }
}
