namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Per-(job, tenant) mutual exclusion. Postgres: pg_try_advisory_lock(hashtext(job||tenant)).
/// </summary>
public interface IJobLock
{
    /// <summary>Try to acquire; returns a disposable that releases on dispose, or null if busy.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string jobName, Guid tenantId, CancellationToken cancellationToken = default);
}
