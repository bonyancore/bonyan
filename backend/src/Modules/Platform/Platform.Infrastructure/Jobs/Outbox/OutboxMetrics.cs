namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Operational metrics for outbox health monitoring.
/// </summary>
public sealed record OutboxMetrics(
    double LagSeconds,
    long PendingCount,
    long TotalAttempts
);
