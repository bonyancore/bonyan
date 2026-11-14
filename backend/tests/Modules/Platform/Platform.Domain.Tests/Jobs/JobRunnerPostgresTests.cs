using System.Data.Common;
using Construction.Platform.Core.Domain.Jobs;
using Construction.Platform.Infrastructure.Jobs.Runner;
using Npgsql;
using Xunit;

namespace Construction.Platform.Domain.Tests.Jobs;

public class LivePgFactAttribute : FactAttribute
{
    public LivePgFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable("LIVE_PG_ENABLED")
            ?? Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Live PostgreSQL tests are disabled. Set LIVE_PG_ENABLED=true to enable.";
        }
    }
}

public sealed class JobRunnerPostgresTests
{
    private const string AppConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=app;Password=app_dev_pw";

    private static Func<CancellationToken, Task<DbConnection>> CreateFactory()
    {
        return async ct =>
        {
            var conn = new NpgsqlConnection(AppConnectionString);
            await conn.OpenAsync(ct);
            return conn;
        };
    }

    [LivePgFact]
    public async Task PostgresJobRunStore_Insert_Update_Get_RoundTrip()
    {
        var store = new PostgresJobRunStore(CreateFactory());
        var tenantId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var runKey = $"test_job:{tenantId:N}:2026-11-14:01";

        var run = new JobRun
        {
            Id = runId,
            TenantId = tenantId,
            Job = "test_job",
            RunKey = runKey,
            Trigger = "schedule",
            Queue = "default",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Attempts = 1
        };

        // 1. Insert
        var inserted = await store.TryInsertAsync(run);
        Assert.True(inserted);

        // 2. Get
        var fetched = await store.GetAsync(tenantId, runId);
        Assert.NotNull(fetched);
        Assert.Equal("running", fetched.Status);
        Assert.Equal(runKey, fetched.RunKey);

        // 3. Update
        fetched.Status = "succeeded";
        fetched.FinishedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(fetched);

        // 4. Verify updated
        var updated = await store.GetAsync(tenantId, runId);
        Assert.NotNull(updated);
        Assert.Equal("succeeded", updated.Status);
        Assert.NotNull(updated.FinishedAt);
    }

    [LivePgFact]
    public async Task PostgresJobRunStore_DuplicateRunKey_ReturnsFalse()
    {
        var store = new PostgresJobRunStore(CreateFactory());
        var tenantId = Guid.NewGuid();
        var runKey = $"unique_job:{tenantId:N}:2026-11-14:02";

        var run1 = new JobRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Job = "unique_job",
            RunKey = runKey,
            Trigger = "schedule",
            Queue = "default",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Attempts = 1
        };

        var run2 = new JobRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Job = "unique_job",
            RunKey = runKey,
            Trigger = "schedule",
            Queue = "default",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Attempts = 1
        };

        var first = await store.TryInsertAsync(run1);
        Assert.True(first, "First run insertion must succeed.");

        var second = await store.TryInsertAsync(run2);
        Assert.False(second, "Second run with identical (tenant_id, job, run_key) must return false (idempotent guard).");
    }

    [LivePgFact]
    public async Task PostgresJobLock_AdvisoryLock_PreventsConcurrentRun()
    {
        var jobLock = new PostgresJobLock(CreateFactory());
        var tenantId = Guid.NewGuid();
        const string jobName = "concurrent_guarded_job";

        // 1. Acquire first lease
        var lease1 = await jobLock.TryAcquireAsync(jobName, tenantId);
        Assert.NotNull(lease1);

        // 2. Attempt second concurrent acquisition with same job and tenant
        var lease2 = await jobLock.TryAcquireAsync(jobName, tenantId);
        Assert.Null(lease2);

        // 3. Release first lease
        await lease1.DisposeAsync();

        // 4. Now acquisition must succeed again
        var lease3 = await jobLock.TryAcquireAsync(jobName, tenantId);
        Assert.NotNull(lease3);
        await lease3.DisposeAsync();
    }

    [LivePgFact]
    public async Task PostgresJobRunStore_TenantIsolation_CannotReadOtherTenantRun()
    {
        var store = new PostgresJobRunStore(CreateFactory());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var run = new JobRun
        {
            Id = runId,
            TenantId = tenantA,
            Job = "isolated_job",
            RunKey = $"isolated_job:{tenantA:N}:01",
            Trigger = "schedule",
            Queue = "default",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Attempts = 1
        };

        await store.TryInsertAsync(run);

        // Tenant B attempts to read Tenant A's run
        var readByB = await store.GetAsync(tenantB, runId);
        Assert.Null(readByB);
    }
}
