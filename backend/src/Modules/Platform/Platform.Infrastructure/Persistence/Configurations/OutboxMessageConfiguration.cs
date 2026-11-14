using Construction.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox", PlatformDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.AggregateType).HasColumnName("aggregate_type").IsRequired();
        builder.Property(x => x.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(x => x.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").IsRequired(false);
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(x => x.DispatchedAt).HasColumnName("dispatched_at").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(x => x.DeadAt).HasColumnName("dead_at").HasColumnType("timestamp with time zone").IsRequired(false);

        builder.HasIndex(x => new { x.NextAttemptAt, x.Id })
            .HasDatabaseName("ix_outbox_pending")
            .HasFilter("dispatched_at IS NULL AND dead_at IS NULL");

        // Includes dead-lettered rows so the per-aggregate NOT EXISTS blocker stays index-backed.
        builder.HasIndex(x => new { x.AggregateId, x.Id })
            .HasDatabaseName("ix_outbox_agg")
            .HasFilter("dispatched_at IS NULL");
    }
}
