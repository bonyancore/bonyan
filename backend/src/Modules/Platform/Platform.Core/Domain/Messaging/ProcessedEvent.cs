namespace Construction.Platform.Core.Domain.Messaging;

/// <summary>
/// Entity representing a processed event in platform.processed_events.
/// Keys: PK(consumer, event_id). Not tenant-scoped as event_ids are globally unique.
/// </summary>
public sealed class ProcessedEvent
{
    public string Consumer { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
