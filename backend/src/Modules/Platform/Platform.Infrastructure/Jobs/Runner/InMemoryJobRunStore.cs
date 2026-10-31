using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>In-memory job-run store: unit tests and explicitly-enabled development mode only.</summary>
public sealed class InMemoryJobRunStore : IJobRunStore
{
    private readonly List<JobRun> _runs = new();
    private readonly object _lock = new();

    public Task<bool> TryInsertAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_runs.Any(r =>
                    r.TenantId == run.TenantId &&
                    string.Equals(r.Job, run.Job, StringComparison.Ordinal) &&
                    string.Equals(r.RunKey, run.RunKey, StringComparison.Ordinal)))
            {
                return Task.FromResult(false);
            }

            _runs.Add(run);
            return Task.FromResult(true);
        }
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

    public Task<JobRun?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_runs.FirstOrDefault(r => r.Id == id && r.TenantId == tenantId));
        }
    }

    public Task<bool> ExistsForWindowAsync(
        Guid tenantId,
        string job,
        string runKey,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_runs.Any(r =>
                r.TenantId == tenantId &&
                string.Equals(r.Job, job, StringComparison.Ordinal) &&
                string.Equals(r.RunKey, runKey, StringComparison.Ordinal)));
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
