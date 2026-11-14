using System.Collections.Concurrent;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

public sealed class InMemoryJobLock : IJobLock
{
    private readonly ConcurrentDictionary<string, byte> _held = new(StringComparer.Ordinal);

    public Task<IAsyncDisposable?> TryAcquireAsync(
        string jobName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{jobName}|{tenantId:N}";
        if (!_held.TryAdd(key, 0))
        {
            return Task.FromResult<IAsyncDisposable?>(null);
        }

        return Task.FromResult<IAsyncDisposable?>(new Releaser(_held, key));
    }

    private sealed class Releaser(ConcurrentDictionary<string, byte> held, string key) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            held.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }
    }
}
