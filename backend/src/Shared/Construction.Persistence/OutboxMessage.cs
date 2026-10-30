namespace Construction.Persistence;

/// <summary>
/// Represents a row in platform.outbox for transactional event publishing and at-least-once ordered delivery.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? DeadAt { get; set; }
}
