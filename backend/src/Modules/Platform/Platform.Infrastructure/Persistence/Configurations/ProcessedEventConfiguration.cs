using Construction.Platform.Core.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events", PlatformDbContext.Schema);
        builder.HasKey(e => new { e.Consumer, e.EventId });
        builder.Property(e => e.Consumer).HasColumnName("consumer").IsRequired();
        builder.Property(e => e.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("now()").IsRequired();
    }
}
