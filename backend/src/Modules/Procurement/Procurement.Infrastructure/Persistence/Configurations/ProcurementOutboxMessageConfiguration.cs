using Construction.Procurement.Core.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProcurementOutboxMessageConfiguration : IEntityTypeConfiguration<ProcurementOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ProcurementOutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TenantId, item.PublishedAt, item.NextAttemptAt });
        builder.Property(item => item.EventType).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.OccurredAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.PublishedAt).HasColumnType("timestamp with time zone");
        builder.Property(item => item.NextAttemptAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(2000);
        builder.ConfigureAudit();
    }
}
