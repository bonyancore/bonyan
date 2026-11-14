using Construction.Kernel;
using Construction.Platform.Core.Application.Outbox;

namespace Construction.Platform.Infrastructure.Application;

/// <summary>
/// In-memory dead-letter store for tests and for an explicitly-enabled development run (§8).
/// It mirrors the PostgreSQL semantics that matter to the endpoints — the conditional
/// <c>resolution = 'open'</c> transition, 404 vs 409, and keyset ordering — so the workflow can be
/// exercised without a database. It is never registered in Production.
/// </summary>
public sealed class InMemoryDeadLetterStore : IDeadLetterStore, IOutboxMetricsStore
{
    private readonly List<DeadOutboxMessageDto> _rows = [];
    private readonly object _lock = new();
    private readonly IClock _clock;
    private double _lagSeconds;
    private long _pending;

    public InMemoryDeadLetterStore(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Test helper: seed a dead letter as produced by the dispatcher after the 5th failure.</summary>
    public DeadOutboxMessageDto Seed(
        Guid tenantId,
        string type = "test.order.placed.v1",
        string? lastError = "application: delivery failed.",
        int attempts = 5,
        DateTimeOffset? deadAt = null)
    {
        var row = new DeadOutboxMessageDto(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            tenantId,
            type,
            "{}",
            "order",
            Guid.CreateVersion7(),
            1,
            attempts,
            lastError,
            "application",
            "InvalidOperationException",
            deadAt ?? _clock.UtcNow,
            "open",
            null,
            null,
            null,
            null);

        lock (_lock)
        {
            _rows.Add(row);
        }

        return row;
    }

    /// <summary>Test helper: what the lag endpoint will report.</summary>
    public void SeedLag(double lagSeconds, long pendingCount)
    {
        _lagSeconds = lagSeconds;
        _pending = pendingCount;
    }

    public Task<IReadOnlyList<DeadOutboxMessageDto>> ListAsync(
        Guid tenantId,
        string? eventType,
        string? resolution,
        DateTimeOffset? cursorDeadAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }
        if (cursorDeadAt.HasValue != cursorId.HasValue)
        {
            throw new ArgumentException("Both cursor values must be supplied together.");
        }

        lock (_lock)
        {
            var query = _rows
                .Where(r => r.TenantId == tenantId)
                .Where(r => eventType is null || r.Type == eventType)
                .Where(r => resolution is null || r.Resolution == resolution)
                .Where(r => !cursorDeadAt.HasValue
                            || r.DeadAt < cursorDeadAt.Value
                            || (r.DeadAt == cursorDeadAt.Value && r.Id.CompareTo(cursorId!.Value) < 0))
                .OrderByDescending(r => r.DeadAt)
                .ThenByDescending(r => r.Id)
                .Take(pageSize + 1)
                .ToList();

            return Task.FromResult<IReadOnlyList<DeadOutboxMessageDto>>(query);
        }
    }

    public Task<DeadOutboxMessageDto?> GetAsync(
        Guid tenantId,
        Guid deadMessageId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.Id == deadMessageId && r.TenantId == tenantId));
        }
    }

    public Task<DeadLetterMutationResult> ReplayAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > DeadLetterService.MaxNoteLength)
        {
            throw new ArgumentException("A replay reason of 1 to 500 characters is required.", nameof(reason));
        }

        return ResolveAsync(tenantId, actorId, deadMessageId, "replayed", reason);
    }

    public Task<DeadLetterMutationResult> DiscardAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Length > DeadLetterService.MaxNoteLength)
        {
            throw new ArgumentException("A discard note of 1 to 500 characters is required.", nameof(note));
        }

        return ResolveAsync(tenantId, actorId, deadMessageId, "discarded", note);
    }

    public Task<OutboxLagDto> GetLagAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OutboxLagDto(Math.Max(0, _lagSeconds), _pending));

    private Task<DeadLetterMutationResult> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string resolution,
        string note)
    {
        lock (_lock)
        {
            var index = _rows.FindIndex(r => r.Id == deadMessageId && r.TenantId == tenantId);
            if (index < 0)
            {
                return Task.FromResult(DeadLetterMutationResult.NotFound());
            }

            var row = _rows[index];
            if (!string.Equals(row.Resolution, "open", StringComparison.Ordinal))
            {
                return Task.FromResult(DeadLetterMutationResult.AlreadyResolved(row.Id));
            }

            var now = _clock.UtcNow;
            _rows[index] = row with
            {
                Resolution = resolution,
                ResolvedBy = actorId,
                ResolvedAt = now,
                ResolutionNote = note.Trim(),
                ReplayedAt = resolution == "replayed" ? now : row.ReplayedAt
            };

            return Task.FromResult(
                new DeadLetterMutationResult(DeadLetterResolutionState.Resolved, row.Id, now));
        }
    }
}
