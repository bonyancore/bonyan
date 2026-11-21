using System.Collections.Concurrent;

namespace Construction.Web.Idempotency;

/// <summary>
/// In-memory idempotency store for tests and explicitly enabled Development runs.
/// It preserves the single-claimant guarantee for one process but is not multi-instance safe;
/// production must use <c>PostgresIdempotencyStore</c>.
/// </summary>
public sealed class InMemoryIdempotencyStore(TimeProvider? timeProvider = null) : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _rows = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public Task<bool> TryClaimAsync(
        IdempotencyScope scope,
        byte[] requestHash,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requestHash);

        var id = Key(scope);
        var now = _time.GetUtcNow();

        lock (_gate)
        {
            if (_rows.TryGetValue(id, out var existing))
            {
                if (existing.ExpiresAt > now)
                {
                    return Task.FromResult(false);
                }

                _rows[id] = Entry.Claim(requestHash, now, lifetime);
                return Task.FromResult(true);
            }

            _rows[id] = Entry.Claim(requestHash, now, lifetime);
            return Task.FromResult(true);
        }
    }

    public Task<IdempotencyRecord?> FindAsync(
        IdempotencyScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (_gate)
        {
            return Task.FromResult(_rows.TryGetValue(Key(scope), out var entry) ? entry.ToRecord() : null);
        }
    }

    public Task<bool> TryTakeOverAsync(
        IdempotencyScope scope,
        TimeSpan staleAfter,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var id = Key(scope);
        var now = _time.GetUtcNow();

        lock (_gate)
        {
            if (!_rows.TryGetValue(id, out var entry) || !entry.IsInProgress)
            {
                return Task.FromResult(false);
            }

            if (now - entry.CreatedAt < staleAfter)
            {
                return Task.FromResult(false);
            }

            _rows[id] = Entry.Claim(entry.RequestHash, now, lifetime);
            return Task.FromResult(true);
        }
    }

    public Task CompleteAsync(
        IdempotencyScope scope,
        int statusCode,
        string? contentType,
        byte[]? body,
        IReadOnlyDictionary<string, string> headers,
        bool tooLarge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var id = Key(scope);

        lock (_gate)
        {
            if (_rows.TryGetValue(id, out var entry))
            {
                entry.MarkCompleted(statusCode, contentType, body, headers, tooLarge);
            }
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(IdempotencyScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (_gate)
        {
            if (_rows.TryGetValue(Key(scope), out var entry) && entry.IsInProgress)
            {
                _rows.TryRemove(Key(scope), out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var now = _time.GetUtcNow();
        var removed = 0;

        lock (_gate)
        {
            foreach (var pair in _rows)
            {
                if (removed >= batchSize)
                {
                    break;
                }

                if (pair.Value.ExpiresAt < now)
                {
                    _rows.TryRemove(pair.Key, out _);
                    removed++;
                }
            }
        }

        return Task.FromResult(removed);
    }

    private static string Key(IdempotencyScope scope) =>
        $"{scope.TenantId:N}|{scope.PrincipalId:N}|{scope.Key}";

    private sealed class Entry
    {
        public byte[] RequestHash { get; private set; } = [];
        public string Status { get; private set; } = IdempotencyRecord.InProgress;
        public int? ResponseStatus { get; private set; }
        public string? ContentType { get; private set; }
        public byte[]? Body { get; private set; }
        public bool TooLarge { get; private set; }
        public IReadOnlyDictionary<string, string> Headers { get; private set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }

        public bool IsInProgress => Status == IdempotencyRecord.InProgress;

        public static Entry Claim(byte[] hash, DateTimeOffset now, TimeSpan lifetime) => new()
        {
            RequestHash = hash,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };

        public void MarkCompleted(
            int status,
            string? contentType,
            byte[]? body,
            IReadOnlyDictionary<string, string> headers,
            bool tooLarge)
        {
            Status = IdempotencyRecord.Completed;
            ResponseStatus = status;
            ContentType = contentType;
            Body = body;
            Headers = headers;
            TooLarge = tooLarge;
        }

        public IdempotencyRecord ToRecord() => new(
            Status,
            RequestHash,
            ResponseStatus,
            ContentType,
            Body,
            TooLarge,
            Headers,
            CreatedAt,
            ExpiresAt);
    }
}
