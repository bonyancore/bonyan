using Construction.Docs.Core.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Docs.Infrastructure.Persistence.Configurations;

internal sealed class DocumentRecordConfiguration : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        builder.ToTable("documents", table =>
        {
            table.HasCheckConstraint("ck_documents_record_type", "length(trim(record_type)) between 1 and 100");
            table.HasCheckConstraint("ck_documents_title", "length(trim(title)) between 1 and 300");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.RecordType, item.LinkedRecordId });
        builder.Property(item => item.RecordType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.LinkedRecordId).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(300).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000);
        ConfigureAudit(builder);
        builder.HasMany(item => item.Versions).WithOne().HasForeignKey(item => new { item.TenantId, item.DocumentRecordId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    internal static void ConfigureAudit<T>(EntityTypeBuilder<T> builder)
        where T : class, Construction.Kernel.Persistence.IAuditableEntity
    {
        builder.Property(entity => entity.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasColumnType("uuid");
        builder.Property(entity => entity.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.UpdatedBy).HasColumnType("uuid");
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions", table =>
        {
            table.HasCheckConstraint("ck_document_versions_version_number", "version_number > 0");
            table.HasCheckConstraint("ck_document_versions_status", "status in ('draft','approved','superseded')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.DocumentRecordId, item.VersionNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DocumentRecordId, item.Status });
        builder.Property(item => item.VersionNumber).IsRequired();
        builder.Property(item => item.FileId).IsRequired();
        builder.Property(item => item.Label).HasMaxLength(200);
        builder.Property(item => item.Status).HasConversion(
            status => status.ToString().ToLowerInvariant(),
            status => Enum.Parse<DocumentVersionStatus>(status, true)).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ApprovedAt).HasColumnType("timestamp with time zone");
        DocumentRecordConfiguration.ConfigureAudit(builder);
    }
}
