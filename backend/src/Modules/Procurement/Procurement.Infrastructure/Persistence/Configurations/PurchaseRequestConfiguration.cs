using Construction.Procurement.Core.Domain.PurchaseRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("purchase_requests", table =>
        {
            table.HasCheckConstraint(
                "ck_purchase_requests_status",
                "status in ('draft', 'submitted', 'approved', 'rejected', 'cancelled')");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.HasAlternateKey(request => new { request.TenantId, request.Id })
            .HasName("ak_purchase_requests_tenant_id_id");

        builder.Property(request => request.ProjectId).IsRequired();
        builder.Property(request => request.RequestedBy).IsRequired();
        builder.Property(request => request.TenantId).IsRequired();
        builder.Property(request => request.RequestNumber).HasMaxLength(80);
        builder.Property(request => request.ApprovalReference).HasMaxLength(200);
        builder.Property(request => request.RejectionReason).HasMaxLength(1000);
        builder.Property(request => request.WorkPackageCode).HasMaxLength(80);
        builder.Property(request => request.WorkPackageName).HasMaxLength(200);
        builder.HasIndex(request => new { request.TenantId, request.RequestNumber }).IsUnique();
        builder.HasIndex(request => new { request.TenantId, request.SourceEventId }).IsUnique()
            .HasFilter("source_event_id IS NOT NULL");
        builder.Property(request => request.Status)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                status => Enum.Parse<PurchaseRequestStatus>(status, ignoreCase: true))
            .HasMaxLength(20)
            .IsRequired();

        ConfigureAuditColumns(builder);

        builder.HasIndex(request => new { request.TenantId, request.ProjectId, request.CreatedAt })
            .HasDatabaseName("ix_purchase_requests_tenant_project_created_at");

        builder.HasMany(request => request.Items)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(request => new { request.TenantId, request.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(request => request.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureAuditColumns(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.Property(request => request.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(request => request.CreatedBy).HasColumnType("uuid");
        builder.Property(request => request.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(request => request.UpdatedBy).HasColumnType("uuid");
    }
}
