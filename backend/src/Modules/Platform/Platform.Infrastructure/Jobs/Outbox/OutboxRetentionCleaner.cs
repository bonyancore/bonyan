namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Retention cleaner job for purging dispatched outbox messages.
/// Default retention window: 14 days. Purges in configurable batches.
/// </summary>
public sealed class OutboxRetentionCleaner
{
    private readonly IOutboxRepository _repository;
    private readonly TimeSpan _retentionWindow;

    public OutboxRetentionCleaner(
        IOutboxRepository repository,
        TimeSpan? retentionWindow = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _retentionWindow = retentionWindow ?? TimeSpan.FromDays(14);
    }

    public async Task<int> RunCleanupAsync(int batchSize = 1000, CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.Subtract(_retentionWindow);
        int totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            int deleted = await _repository.PurgeDispatchedAsync(threshold, batchSize, cancellationToken).ConfigureAwait(false);
            totalDeleted += deleted;

            if (deleted < batchSize)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
