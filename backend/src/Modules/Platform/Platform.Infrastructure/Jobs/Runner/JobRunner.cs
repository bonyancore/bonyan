using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Executes a single job for one tenant: advisory lock → job_runs start → tenant scope → timeout → finish.
/// </summary>
public sealed class JobRunner
{
    private readonly IJobRunStore _runStore;
    private readonly IJobLock _jobLock;
    private readonly ITenantSetter _tenantSetter;
    private readonly ITenantJobEnablement _enablement;
    private readonly IClock _clock;

    public JobRunner(
        IJobRunStore runStore,
        IJobLock jobLock,
        ITenantSetter tenantSetter,
        ITenantJobEnablement enablement,
        IClock? clock = null)
    {
        _runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
        _jobLock = jobLock ?? throw new ArgumentNullException(nameof(jobLock));
        _tenantSetter = tenantSetter ?? throw new ArgumentNullException(nameof(tenantSetter));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _clock = clock ?? new SystemClock();
    }

    public async Task<JobRun> RunAsync(
        IJob job,
        TenantId tenantId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Job requires a non-empty tenant context (fail closed).");
        }

        if (!JobQueues.IsKnown(job.Queue))
        {
            throw new InvalidOperationException($"Unknown job queue '{job.Queue}'.");
        }

        if (!_enablement.IsEnabled(tenantId.Value, job.JobName))
        {
            var skippedDisabled = NewRun(job, tenantId, JobRunStatus.Skipped, errorCode: "platform.job_disabled");
            skippedDisabled.FinishedAt = _clock.UtcNow;
            await _runStore.InsertAsync(skippedDisabled, cancellationToken).ConfigureAwait(false);
            return skippedDisabled;
        }

        await using var lease = await _jobLock.TryAcquireAsync(job.JobName, tenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (lease is null)
        {
            var skipped = NewRun(job, tenantId, JobRunStatus.Skipped, errorCode: "platform.job_overlap");
            skipped.FinishedAt = _clock.UtcNow;
            await _runStore.InsertAsync(skipped, cancellationToken).ConfigureAwait(false);
            return skipped;
        }

        var run = NewRun(job, tenantId, JobRunStatus.Running);
        await _runStore.InsertAsync(run, cancellationToken).ConfigureAwait(false);

        using var tenantScope = _tenantSetter.BeginScope(tenantId);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (job.Timeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(job.Timeout);
        }

        var context = new JobContext(tenantId, run.Id, _clock, timeZoneId);

        try
        {
            await job.RunAsync(context, timeoutCts.Token).ConfigureAwait(false);
            run.Status = JobRunStatus.Succeeded;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            run.Status = JobRunStatus.TimedOut;
            run.ErrorCode = "platform.job_timed_out";
        }
        catch (Exception)
        {
            run.Status = JobRunStatus.Failed;
            run.ErrorCode = "platform.job_failed";
            throw;
        }
        finally
        {
            run.FinishedAt = _clock.UtcNow;
            await _runStore.UpdateAsync(run, CancellationToken.None).ConfigureAwait(false);
        }

        return run;
    }

    private JobRun NewRun(IJob job, TenantId tenantId, string status, string? errorCode = null) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId.Value,
        Job = job.JobName,
        Queue = job.Queue,
        StartedAt = _clock.UtcNow,
        Status = status,
        ErrorCode = errorCode,
        Attempts = 1
    };
}
