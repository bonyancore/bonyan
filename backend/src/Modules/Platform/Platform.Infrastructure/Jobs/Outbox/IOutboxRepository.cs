using Construction.Persistence;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkDispatchedAsync(
        Guid messageId,
        DateTimeOffset dispatchedAt,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        OutboxMessage message,
        int attempts,
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        bool isDead,
        string errorClass,
        string lastError,
        string errorCategory,
        CancellationToken cancellationToken = default);

    Task<OutboxMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    Task<int> PurgeDispatchedAsync(
        DateTimeOffset olderThan,
        int batchSize,
        CancellationToken cancellationToken = default);
}
