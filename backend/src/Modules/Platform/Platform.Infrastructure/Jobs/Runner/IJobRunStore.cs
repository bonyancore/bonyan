using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

public interface IJobRunStore
{
    /// <summary>
    /// Inserts the run. Returns false when (tenant, job, run_key) already exists, which is how a
    /// duplicate window is suppressed across workers (BR-PLT-140); the caller then does not execute.
    /// </summary>
    Task<bool> TryInsertAsync(JobRun run, CancellationToken cancellationToken = default);

    Task UpdateAsync(JobRun run, CancellationToken cancellationToken = default);

    Task<bool> ExistsForWindowAsync(
        Guid tenantId,
        string job,
        string runKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one run inside the tenant's RLS scope. <c>platform.job_runs</c> has RLS forced, so the
    /// tenant is a required argument: a lookup that did not set <c>app.tenant_id</c> would silently
    /// see no rows.
    /// </summary>
    Task<JobRun?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}
