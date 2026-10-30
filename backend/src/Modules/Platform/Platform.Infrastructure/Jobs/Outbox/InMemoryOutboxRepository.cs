using Construction.Persistence;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Thread-safe in-memory implementation of IOutboxRepository for unit and concurrency tests.
/// Faithful to PostgreSQL SKIP LOCKED, lease expiry, and per-aggregate ordering constraints.
/// </summary>
public sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly List<OutboxMessage> _messages = new();
    private readonly object _lock = new();

    public void Add(OutboxMessage message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }

    public void AddRange(IEnumerable<OutboxMessage> messages)
    {
        lock (_lock)
        {
            _messages.AddRange(messages);
        }
    }

    public IReadOnlyList<OutboxMessage> GetAllMessages()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    /// <summary>
    /// Test helper: expire all active leases so another worker can reclaim.
    /// </summary>
    public void ExpireAllLeases()
    {
        lock (_lock)
        {
            var past = DateTimeOffset.UtcNow.AddSeconds(-1);
            foreach (var message in _messages.Where(m => m.LockedUntil != null))
            {
                message.LockedUntil = past;
                message.LockedBy = null;
            }
        }
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var claimed = new List<OutboxMessage>();

            // Find all pending and unlocked candidates
            var candidates = _messages
                .Where(m => m.DispatchedAt == null && m.DeadAt == null)
                .Where(m => m.NextAttemptAt <= now)
                .Where(m => m.LockedUntil == null || m.LockedUntil < now)
                .OrderBy(m => m.Id)
                .ToList();

            foreach (var cand in candidates)
            {
                if (claimed.Count >= batchSize) break;

                // Per-aggregate ordering (Runbook §6 default #2): any earlier row that is
                // not yet successfully dispatched blocks — including leased and dead-lettered.
                bool hasEarlierUndispatched = _messages.Any(p =>
                    p.AggregateId == cand.AggregateId &&
                    p.Id.CompareTo(cand.Id) < 0 &&
                    p.DispatchedAt == null);

                if (!hasEarlierUndispatched)
                {
                    cand.LockedBy = workerId;
                    cand.LockedUntil = now.Add(leaseDuration);
                    claimed.Add(cand);
                }
            }

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(claimed);
        }
    }

    public Task MarkDispatchedAsync(
        Guid messageId,
        DateTimeOffset dispatchedAt,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var msg = _messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.DispatchedAt = dispatchedAt;
                msg.LockedBy = null;
                msg.LockedUntil = null;
            }

            return Task.CompletedTask;
        }
    }

    public Task MarkFailedAsync(
        Guid messageId,
        int attempts,
        DateTimeOffset nextAttemptAt,
        bool isDead,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var msg = _messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.Attempts = attempts;
                msg.NextAttemptAt = nextAttemptAt;
                msg.DeadAt = isDead ? DateTimeOffset.UtcNow : null;
                msg.LockedBy = null;
                msg.LockedUntil = null;
            }

            return Task.CompletedTask;
        }
    }

    public Task<OutboxMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var pending = _messages
                .Where(m => m.DispatchedAt == null && m.DeadAt == null)
                .ToList();

            if (pending.Count == 0)
            {
                return Task.FromResult(new OutboxMetrics(0, 0, 0));
            }

            var oldest = pending.Min(m => m.OccurredAt);
            var lag = Math.Max(0, (now - oldest).TotalSeconds);
            var attempts = pending.Sum(m => (long)m.Attempts);

            return Task.FromResult(new OutboxMetrics(lag, pending.Count, attempts));
        }
    }

    public Task<int> PurgeDispatchedAsync(
        DateTimeOffset olderThan,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var toPurge = _messages
                .Where(m => m.DispatchedAt != null && m.DispatchedAt < olderThan)
                .Take(batchSize)
                .ToList();

            foreach (var item in toPurge)
            {
                _messages.Remove(item);
            }

            return Task.FromResult(toPurge.Count);
        }
    }
}
