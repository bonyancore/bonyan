namespace Construction.Platform.Core.Domain.Jobs;

/// <summary>
/// Row in platform.job_runs — one execution attempt of a scheduled/background job.
/// </summary>
public sealed class JobRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Job { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// Idempotency key for the run window: <c>{job}:{tenant}:{window}</c> (BR-PLT-140).
    /// Unique per (tenant, job) so a second enqueue of the same window is a no-op.
    /// </summary>
    public string RunKey { get; set; } = string.Empty;

    /// <summary>schedule | manual | retry</summary>
    public string Trigger { get; set; } = JobRunTrigger.Schedule;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = JobRunStatus.Running;
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; } = 1;
}

public static class JobRunStatus
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string TimedOut = "timed_out";
    public const string Skipped = "skipped";
}

public static class JobRunTrigger
{
    public const string Schedule = "schedule";
    public const string Manual = "manual";
    public const string Retry = "retry";
}
