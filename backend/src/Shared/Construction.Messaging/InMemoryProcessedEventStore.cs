using System.Collections.Concurrent;

namespace Construction.Messaging;

/// <summary>
/// In-memory implementation of IProcessedEventStore for unit tests and fast in-memory execution.
/// Thread-safe and atomic per consumer + eventId pair.
/// </summary>
public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private readonly ConcurrentDictionary<(string Consumer, Guid EventId), DateTimeOffset> _committedEvents = new();

    public IReadOnlyCollection<(string Consumer, Guid EventId)> CommittedEvents => _committedEvents.Keys.ToList();

    public bool IsProcessed(string consumer, Guid eventId) => _committedEvents.ContainsKey((consumer, eventId));

    public void Clear() => _committedEvents.Clear();

    public Task<IConsumerTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IConsumerTransactionContext>(new InMemoryTransactionContext(this));
    }

    private sealed class InMemoryTransactionContext : IConsumerTransactionContext
    {
        private readonly InMemoryProcessedEventStore _store;
        private readonly List<(string Consumer, Guid EventId)> _stagedEvents = new();
        private bool _completed;

        public InMemoryTransactionContext(InMemoryProcessedEventStore store)
        {
            _store = store;
        }

        public Task<bool> TryRecordProcessedAsync(string consumer, Guid eventId, CancellationToken cancellationToken = default)
        {
            var key = (consumer, eventId);
            lock (_store._committedEvents)
            {
                if (_store._committedEvents.ContainsKey(key) || _stagedEvents.Contains(key))
                {
                    return Task.FromResult(false);
                }

                _stagedEvents.Add(key);
                return Task.FromResult(true);
            }
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (!_completed)
            {
                lock (_store._committedEvents)
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var key in _stagedEvents)
                    {
                        _store._committedEvents[key] = now;
                    }
                }
                _completed = true;
            }

            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!_completed)
            {
                _stagedEvents.Clear();
                _completed = true;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                _stagedEvents.Clear();
                _completed = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
