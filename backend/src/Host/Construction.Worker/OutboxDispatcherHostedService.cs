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
                var tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
                var repo = scope.ServiceProvider.GetService<IOutboxRepository>();
                var registry = scope.ServiceProvider.GetService<IOutboxConsumerRegistry>();

                if (repo is null)
                {
                    var db = scope.ServiceProvider.GetService<PlatformDbContext>();
                    if (db is null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    repo = new PostgresOutboxRepository(async ct =>
                    {
                        var conn = db.Database.GetDbConnection();
                        if (conn.State != System.Data.ConnectionState.Open)
                        {
                            await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                        }

                        return conn;
                    });
                }

                OutboxMessageDeliveryHandler handler = registry != null
                    ? registry.DeliverAsync
                    : (_, _) => Task.CompletedTask;

                var dispatcher = new OutboxDispatcher(
                    repo,
                    tenantSetter,
                    handler,
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
