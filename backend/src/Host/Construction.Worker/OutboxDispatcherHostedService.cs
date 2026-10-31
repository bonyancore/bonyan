using Construction.Kernel.Tenancy;
using Construction.Platform.Infrastructure.Jobs.Outbox;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construction.Worker;

/// <summary>PLT-202 hand-off: Worker loop claiming and delivering outbox batches.</summary>
public sealed class OutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetService<PlatformDbContext>();
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();

                if (db is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var repo = new PostgresOutboxRepository(async ct =>
                {
                    var conn = db.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                    }

                    return conn;
                });

                // Delivery wiring for typed consumers lands with module consumers; no-op mark path for host boot.
                var dispatcher = new OutboxDispatcher(
                    repo,
                    tenantSetter,
                    (_, _) => Task.CompletedTask,
                    workerId: Environment.MachineName);

                await dispatcher.ProcessBatchAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatcher tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }
}
