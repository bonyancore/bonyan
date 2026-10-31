using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

public sealed class InMemoryJobRunStore : IJobRunStore
{
    private readonly List<JobRun> _runs = new();
    private readonly object _lock = new();

    public Task InsertAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _runs.Add(run);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var idx = _runs.FindIndex(r => r.Id == run.Id);
            if (idx >= 0)
            {
                _runs[idx] = run;
            }
        }

        return Task.CompletedTask;
    }

    public Task<JobRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_runs.FirstOrDefault(r => r.Id == id));
        }
    }

    public IReadOnlyList<JobRun> ListAll()
    {
        lock (_lock)
        {
            return _runs.ToList();
        }
    }
}
