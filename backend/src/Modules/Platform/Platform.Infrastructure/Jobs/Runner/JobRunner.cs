using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Executes a single job for one tenant: schedule window → advisory lock → job_runs start → tenant
/// scope → timeout → finish. One window executes at most once per (tenant, job) (BR-PLT-140).
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

    /// <summary>
    /// Runs a job because a scheduler tick decided it is due. Returns <c>null</c> when the job is not
    /// due in the tenant's time zone or its window already ran (so no row is written for every tick).
    /// </summary>
    public async Task<JobRun?> TryRunScheduledAsync(
        IJob job,
        TenantId tenantId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        EnsureTenant(tenantId);

        if (job.Cron is null)
        {
            return await RunAsync(job, tenantId, timeZoneId, cancellationToken).ConfigureAwait(false);
        }

        var schedule = CronSchedule.Parse(job.Cron);
        var now = _clock.UtcNow;
        if (!schedule.IsDue(now, timeZoneId))
        {
            return null;
        }

        var runKey = WindowKey(job, tenantId, schedule.WindowStart(now, timeZoneId));
        if (await _runStore.ExistsForWindowAsync(tenantId.Value, job.JobName, runKey, cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        return await ExecuteAsync(job, tenantId, timeZoneId, runKey, JobRunTrigger.Schedule, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Runs a job immediately (manual trigger, retry or an on-demand job).</summary>
    public async Task<JobRun> RunAsync(
        IJob job,
        TenantId tenantId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        EnsureTenant(tenantId);

        var runKey = $"{job.JobName}:{tenantId.Value:N}:once:{Guid.CreateVersion7():N}";
        var run = await ExecuteAsync(job, tenantId, timeZoneId, runKey, JobRunTrigger.Manual, cancellationToken)
            .ConfigureAwait(false);

        return run!;
    }

    private async Task<JobRun?> ExecuteAsync(
        IJob job,
        TenantId tenantId,
        string? timeZoneId,
        string runKey,
        string trigger,
        CancellationToken cancellationToken)
    {
        if (!JobQueues.IsKnown(job.Queue))
        {
            throw new InvalidOperationException($"Unknown job queue '{job.Queue}'.");
        }

        if (!_enablement.IsEnabled(tenantId.Value, job.JobName))
        {
            return await RecordSkippedAsync(
                job, tenantId, runKey, trigger, "platform.job_disabled", cancellationToken).ConfigureAwait(false);
        }

        await using var lease = await _jobLock.TryAcquireAsync(job.JobName, tenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (lease is null)
        {
            return await RecordSkippedAsync(
                job, tenantId, runKey, trigger, "platform.job_overlap", cancellationToken).ConfigureAwait(false);
        }

        var run = NewRun(job, tenantId, runKey, trigger, JobRunStatus.Running);

        // The unique (tenant, job, run_key) index decides the race between two workers.
        if (!await _runStore.TryInsertAsync(run, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

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

    private async Task<JobRun> RecordSkippedAsync(
        IJob job,
        TenantId tenantId,
        string runKey,
        string trigger,
        string errorCode,
        CancellationToken cancellationToken)
    {
        // A skipped run must not collide with the real window row, so it carries its own suffix.
        var skipped = NewRun(
            job,
            tenantId,
            $"{runKey}:skipped:{Guid.CreateVersion7():N}",
            trigger,
            JobRunStatus.Skipped,
            errorCode);
        skipped.FinishedAt = _clock.UtcNow;
        await _runStore.TryInsertAsync(skipped, cancellationToken).ConfigureAwait(false);
        return skipped;
    }

    private static string WindowKey(IJob job, TenantId tenantId, DateTimeOffset windowStart) =>
        $"{job.JobName}:{tenantId.Value:N}:{windowStart:yyyyMMddTHHmm}";

    private static void EnsureTenant(TenantId tenantId)
    {
        if (tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Job requires a non-empty tenant context (fail closed).");
        }
    }

    private JobRun NewRun(
        IJob job,
        TenantId tenantId,
        string runKey,
        string trigger,
        string status,
        string? errorCode = null) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId.Value,
        Job = job.JobName,
        RunKey = runKey,
        Trigger = trigger,
        Queue = job.Queue,
        StartedAt = _clock.UtcNow,
        Status = status,
        ErrorCode = errorCode,
        Attempts = 1
    };
}
