namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>Sample Platform job for PLT-203 done-when: runs per tenant and writes job_runs.</summary>
public sealed class PlatformHeartbeatJob : IJob
{
    public string ModuleCode => "platform";
    public string JobName => "platform.heartbeat";
    public string Queue => JobQueues.Default;
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public string? Cron => "*/5 * * * *";

    public Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // No side effects beyond job_runs bookkeeping owned by JobRunner.
        return Task.CompletedTask;
    }
}
