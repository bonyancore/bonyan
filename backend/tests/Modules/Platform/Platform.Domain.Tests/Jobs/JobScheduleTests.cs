using System.Globalization;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Jobs;
using Construction.Platform.Infrastructure.Jobs.Runner;
using Xunit;

namespace Construction.Platform.Domain.Tests.Jobs;

/// <summary>
/// PLT-203 schedule semantics: only "M H * * *" is supported (tenant time zone) and one window runs
/// at most once per (tenant, job) (BR-PLT-140). Database persistence of job_runs is NOT covered here.
/// </summary>
public sealed class JobScheduleTests
{
    // Fixed-offset zone (UTC+3, no DST) so the assertions are deterministic.
    private const string Riyadh = "Asia/Riyadh";

    private sealed class MutableClock(DateTimeOffset start) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = start;
    }

    private sealed class FixedCronJob : IJob
    {
        public string ModuleCode => "platform";
        public string JobName => "platform.scheduled";
        public string Queue => JobQueues.Default;
        public TimeSpan Timeout => TimeSpan.FromSeconds(5);
        public string? Cron { get; init; } = "0 3 * * *";
        public int Runs { get; private set; }

        public Task RunAsync(JobContext context, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    private static (JobRunner Runner, InMemoryJobRunStore Store, MutableClock Clock) CreateSut(
        DateTimeOffset now)
    {
        var store = new InMemoryJobRunStore();
        var clock = new MutableClock(now);
        var runner = new JobRunner(
            store,
            new InMemoryJobLock(),
            new AsyncLocalTenantContext(),
            new DictionaryTenantJobEnablement(),
            clock);
        return (runner, store, clock);
    }

    [Theory]
    [InlineData("* * * * *")]      // minute must be numeric
    [InlineData("*/5 * * * *")]    // step not supported
    [InlineData("0 3 * * 1")]      // day-of-week not supported
    [InlineData("0 3 1 * *")]      // day-of-month not supported
    [InlineData("0 3 * *")]        // four fields
    [InlineData("60 3 * * *")]     // minute out of range
    [InlineData("0 24 * * *")]     // hour out of range
    [InlineData("")]
    public void Parse_UnsupportedExpressions_ThrowNotSupported(string expression)
    {
        Assert.Throws<NotSupportedException>(() => CronSchedule.Parse(expression));
        Assert.Throws<NotSupportedException>(() => CronSchedule.Parse(null));
    }

    [Fact]
    public void Parse_AcceptsNumericMinuteAndHour()
    {
        var schedule = CronSchedule.Parse("45 6 * * *");

        Assert.Equal(45, schedule.Minute);
        Assert.Equal(6, schedule.Hour);
    }

    [Fact]
    public void IsDue_UsesTheTenantTimeZone()
    {
        var schedule = CronSchedule.Parse("0 3 * * *");

        // 00:00Z is 03:00 in Riyadh (UTC+3): due. 00:30Z is 03:30: not due.
        Assert.True(schedule.IsDue(Instant("2026-11-01T00:00:00Z"), Riyadh));
        Assert.False(schedule.IsDue(Instant("2026-11-01T00:30:00Z"), Riyadh));

        // In UTC the same hour-3 schedule fires at 03:00Z instead.
        Assert.True(schedule.IsDue(Instant("2026-11-01T03:00:00Z"), "UTC"));
        Assert.False(schedule.IsDue(Instant("2026-11-01T00:00:00Z"), "UTC"));
    }

    [Fact]
    public void WindowStart_IsTheLocalWindowInUtc()
    {
        var schedule = CronSchedule.Parse("0 3 * * *");

        var window = schedule.WindowStart(Instant("2026-11-01T00:02:00Z"), Riyadh);

        Assert.Equal(Instant("2026-11-01T00:00:00Z"), window);
    }

    [Fact]
    public async Task ScheduledJob_DoesNotRunOutsideItsMinute()
    {
        var (runner, store, _) = CreateSut(Instant("2026-11-01T00:30:00Z"));
        var job = new FixedCronJob();

        var run = await runner.TryRunScheduledAsync(job, new TenantId(Guid.CreateVersion7()), Riyadh);

        Assert.Null(run);
        Assert.Equal(0, job.Runs);
        Assert.Empty(store.ListAll());
    }

    [Fact]
    public async Task ScheduledJob_RunsOncePerWindow()
    {
        var (runner, store, clock) = CreateSut(Instant("2026-11-01T00:00:00Z"));
        var job = new FixedCronJob();
        var tenant = new TenantId(Guid.CreateVersion7());

        var first = await runner.TryRunScheduledAsync(job, tenant, Riyadh);

        Assert.NotNull(first);
        Assert.Equal(JobRunStatus.Succeeded, first!.Status);
        Assert.Equal(JobRunTrigger.Schedule, first.Trigger);
        // The window is 03:00 in Riyadh == 00:00Z, so the key carries the UTC window instant.
        Assert.Contains("20261101T0000", first.RunKey, StringComparison.Ordinal);
        Assert.Equal(1, job.Runs);

        // A second tick inside the same minute must not execute again.
        clock.UtcNow = Instant("2026-11-01T00:00:30Z");
        var second = await runner.TryRunScheduledAsync(job, tenant, Riyadh);

        Assert.Null(second);
        Assert.Equal(1, job.Runs);
        Assert.Single(store.ListAll());
    }

    [Fact]
    public async Task ScheduledJob_RunsAgainInTheNextDayWindow()
    {
        var (runner, store, clock) = CreateSut(Instant("2026-11-01T00:00:00Z"));
        var job = new FixedCronJob();
        var tenant = new TenantId(Guid.CreateVersion7());

        await runner.TryRunScheduledAsync(job, tenant, Riyadh);
        clock.UtcNow = Instant("2026-11-02T00:00:00Z");
        await runner.TryRunScheduledAsync(job, tenant, Riyadh);

        Assert.Equal(2, job.Runs);
        Assert.Equal(2, store.ListAll().Count);
    }

    [Fact]
    public async Task ManualRuns_AreNotDeduplicated()
    {
        var (runner, store, _) = CreateSut(Instant("2026-11-01T00:00:00Z"));
        var job = new FixedCronJob { Cron = null };
        var tenant = new TenantId(Guid.CreateVersion7());

        await runner.RunAsync(job, tenant, Riyadh);
        await runner.RunAsync(job, tenant, Riyadh);

        Assert.Equal(2, job.Runs);
        Assert.Equal(2, store.ListAll().Count);
        Assert.All(store.ListAll(), r => Assert.Equal(JobRunTrigger.Manual, r.Trigger));
    }

    [Fact]
    public async Task UnsupportedCron_ThrowsNotSupportedFromTheRunner()
    {
        var (runner, _, _) = CreateSut(Instant("2026-11-01T00:00:00Z"));
        var job = new FixedCronJob { Cron = "*/5 * * * *" };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => runner.TryRunScheduledAsync(job, new TenantId(Guid.CreateVersion7()), Riyadh));
    }

    private static DateTimeOffset Instant(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
