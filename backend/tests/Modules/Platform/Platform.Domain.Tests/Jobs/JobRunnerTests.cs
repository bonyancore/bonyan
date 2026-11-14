using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Jobs;
using Construction.Platform.Infrastructure.Jobs.Runner;
using Xunit;
using System.Globalization;

namespace Construction.Platform.Domain.Tests.Jobs;

public sealed class JobRunnerTests
{
    private sealed class RecordingJob : IJob
    {
        public string ModuleCode { get; init; } = "platform";
        public string JobName { get; init; } = "platform.test";
        public string Queue { get; init; } = JobQueues.Default;
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
        public string? Cron => null;
        public int Runs { get; private set; }
        public Func<JobContext, CancellationToken, Task>? Body { get; init; }

        public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
        {
            Runs++;
            if (Body is not null)
            {
                await Body(context, cancellationToken);
            }
        }
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-30T12:00:00Z", CultureInfo.InvariantCulture);
    }

    private static (JobRunner Runner, InMemoryJobRunStore Store, InMemoryJobLock Lock, AsyncLocalTenantContext Tenant) CreateSut(
        ITenantJobEnablement? enablement = null,
        IClock? clock = null)
    {
        var store = new InMemoryJobRunStore();
        var jobLock = new InMemoryJobLock();
        var tenant = new AsyncLocalTenantContext();
        var runner = new JobRunner(
            store,
            jobLock,
            tenant,
            enablement ?? new DictionaryTenantJobEnablement(),
            clock ?? new MutableClock());
        return (runner, store, jobLock, tenant);
    }

    [Fact]
    public async Task RunAsync_WritesSucceededJobRun()
    {
        var (runner, store, _, _) = CreateSut();
        var job = new RecordingJob();
        var tenantId = new TenantId(Guid.CreateVersion7());

        var run = await runner.RunAsync(job, tenantId);

        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(1, job.Runs);
        Assert.Single(store.ListAll());
        Assert.Equal(tenantId.Value, store.ListAll()[0].TenantId);
    }

    [Fact]
    public async Task RunAsync_EmptyTenant_ThrowsFailClosed()
    {
        var (runner, _, _, _) = CreateSut();
        var job = new RecordingJob();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(job, new TenantId(Guid.Empty)));
    }

    [Fact]
    public async Task RunAsync_Timeout_MarksTimedOut()
    {
        var (runner, store, _, _) = CreateSut();
        var job = new RecordingJob
        {
            Timeout = TimeSpan.FromMilliseconds(50),
            Body = async (_, ct) =>
            {
                // Wait until the runner's timeout cancels — do not use Delay to fake clock.
                await Task.Delay(Timeout.Infinite, ct);
            }
        };

        var run = await runner.RunAsync(job, new TenantId(Guid.CreateVersion7()));

        Assert.Equal(JobRunStatus.TimedOut, run.Status);
        Assert.Equal("platform.job_timed_out", run.ErrorCode);
        Assert.Equal(JobRunStatus.TimedOut, store.ListAll().Single().Status);
    }

    [Fact]
    public async Task TwoWorkers_SameJobTenant_SecondIsSkipped()
    {
        var store = new InMemoryJobRunStore();
        var jobLock = new InMemoryJobLock();
        var tenantCtx = new AsyncLocalTenantContext();
        var clock = new MutableClock();
        var enablement = new DictionaryTenantJobEnablement();

        var runner1 = new JobRunner(store, jobLock, tenantCtx, enablement, clock);
        var runner2 = new JobRunner(store, jobLock, tenantCtx, enablement, clock);

        var tenantId = new TenantId(Guid.CreateVersion7());
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var job = new RecordingJob
        {
            Body = async (_, ct) =>
            {
                await gate.Task.WaitAsync(ct);
            }
        };

        var first = runner1.RunAsync(job, tenantId);
        await Task.Delay(20); // allow first to acquire lock (not simulating job duration)
        var second = await runner2.RunAsync(job, tenantId);

        Assert.Equal(JobRunStatus.Skipped, second.Status);
        Assert.Equal("platform.job_overlap", second.ErrorCode);

        gate.SetResult();
        var firstRun = await first;
        Assert.Equal(JobRunStatus.Succeeded, firstRun.Status);
        Assert.Equal(1, job.Runs);
    }

    [Fact]
    public async Task DisabledForTenant_Skipped()
    {
        var tenantId = Guid.CreateVersion7();
        var enablement = new DictionaryTenantJobEnablement([(tenantId, "platform.test")]);
        var (runner, store, _, _) = CreateSut(enablement);
        var job = new RecordingJob();

        var run = await runner.RunAsync(job, new TenantId(tenantId));

        Assert.Equal(JobRunStatus.Skipped, run.Status);
        Assert.Equal("platform.job_disabled", run.ErrorCode);
        Assert.Equal(0, job.Runs);
        Assert.Single(store.ListAll());
    }

    [Fact]
    public void ModuleSwitchedOff_JobsNotRegisteredInCatalog()
    {
        var jobs = new IJob[] { new PlatformHeartbeatJob() };
        var catalogOff = new JobCatalog(jobs, new ModuleJobOptions { Platform = false });
        var catalogOn = new JobCatalog(jobs, new ModuleJobOptions { Platform = true });

        Assert.Empty(catalogOff.Jobs);
        Assert.Single(catalogOn.Jobs);
    }

    [Fact]
    public void ModuleJobOptions_IsEnabled_RespectsFlags()
    {
        var opts = new ModuleJobOptions { Platform = true, Cost = false, Cash = true };
        Assert.True(opts.IsEnabled("platform"));
        Assert.False(opts.IsEnabled("cost"));
        Assert.True(opts.IsEnabled("cash"));
        Assert.False(opts.IsEnabled("unknown"));
    }
}

