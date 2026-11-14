using Construction.Kernel.Tenancy;
using Construction.Platform.Infrastructure.Jobs.Runner;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construction.Worker;

/// <summary>
/// Polls registered jobs and runs them per active tenant (tenant timezone reserved for cron evaluation).
/// </summary>
public sealed class JobSchedulerHostedService(
    JobCatalog catalog,
    JobRunner runner,
    IServiceScopeFactory scopeFactory,
    ILogger<JobSchedulerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        if (catalog.Jobs.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<PlatformDbContext>();
        List<(Guid Id, string? TimeZone)> tenants;

        if (db is null)
        {
            return;
        }

        var tenantRows = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Status == "active" || t.Status == "trial")
            .Select(t => new { t.Id, t.TimeZone })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        tenants = tenantRows.Select(t => (t.Id, (string?)t.TimeZone)).ToList();

        foreach (var (tenantId, tz) in tenants)
        {
            foreach (var job in catalog.Jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await runner.RunAsync(job, new TenantId(tenantId), tz, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Job {Job} failed for tenant {TenantId}", job.JobName, tenantId);
                }
            }
        }
    }
}
