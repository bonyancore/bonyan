using Construction.Kernel.Tenancy;
using Construction.Kernel;
using Construction.Persistence;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Function delegate to dispatch a raw outbox message to its registered event consumer.
/// Receives the OutboxMessage and CancellationToken.
/// </summary>
public delegate Task OutboxMessageDeliveryHandler(OutboxMessage message, CancellationToken cancellationToken);

/// <summary>
/// Worker dispatcher loop implementing PLT-202 claim -> deliver -> mark lifecycle.
/// </summary>
public sealed class OutboxDispatcher
{
    private readonly IOutboxRepository _repository;
    private readonly ITenantSetter _tenantSetter;
    private readonly OutboxMessageDeliveryHandler _deliveryHandler;
    private readonly string _workerId;
    private readonly IClock _clock;

    public OutboxDispatcher(
        IOutboxRepository repository,
        ITenantSetter tenantSetter,
        OutboxMessageDeliveryHandler deliveryHandler,
        string? workerId = null,
        IClock? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tenantSetter = tenantSetter ?? throw new ArgumentNullException(nameof(tenantSetter));
        _deliveryHandler = deliveryHandler ?? throw new ArgumentNullException(nameof(deliveryHandler));
        _workerId = workerId ?? $"worker-{Guid.NewGuid():N}";
        _clock = clock ?? new SystemClock();
    }

    /// <summary>
    /// Executes one round of claiming and delivering outbox messages.
    /// Returns the number of messages processed in this round.
    /// </summary>
    public async Task<int> ProcessBatchAsync(
        int batchSize = 50,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default)
    {
        var lease = leaseDuration ?? TimeSpan.FromSeconds(60);

        // 1. Claim batch in short transaction with SKIP LOCKED
        var batch = await _repository.ClaimBatchAsync(batchSize, _workerId, lease, cancellationToken).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            return 0;
        }

        // 2. Deliver each message outside the claim transaction
        foreach (var message in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // S2: Set ambient tenant context from the envelope/message tenantId
                using var tenantScope = _tenantSetter.BeginScope(new TenantId(message.TenantId));

                // Deliver to consumer(s)
                await _deliveryHandler(message, cancellationToken).ConfigureAwait(false);

                // Success: mark dispatched
                await _repository.MarkDispatchedAsync(message.Id, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Failure: calculate next attempt according to backoff schedule
                var nextAttempt = message.Attempts + 1;
                var (nextDelay, isDead) = CalculateBackoff(nextAttempt);
                var failedAt = _clock.UtcNow;
                var nextAttemptAt = failedAt.Add(nextDelay);
                var (category, errorClass, summary) = DeadLettering.Summarise(exception);

                await _repository.MarkFailedAsync(
                    message,
                    nextAttempt,
                    failedAt,
                    nextAttemptAt,
                    isDead,
                    errorClass,
                    summary,
                    category,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return batch.Count;
    }

    /// <summary>
    /// Backoff schedule per Brief §2.5 / Recipe PLT-202 / PLT-204:
    /// Attempt 1 -> +10s
    /// Attempt 2 -> +1m
    /// Attempt 3 -> +5m
    /// Attempt 4 -> +30m
    /// Attempt 5 -> +2h
    /// After 5th failure -> dead letter
    /// </summary>
    public static (TimeSpan Delay, bool IsDead) CalculateBackoff(int attempt)
    {
        return attempt switch
        {
            1 => (TimeSpan.FromSeconds(10), false),
            2 => (TimeSpan.FromMinutes(1), false),
            3 => (TimeSpan.FromMinutes(5), false),
            4 => (TimeSpan.FromMinutes(30), false),
            5 => (TimeSpan.FromHours(2), true),
            _ => (TimeSpan.FromHours(2), true)
        };
    }
}
