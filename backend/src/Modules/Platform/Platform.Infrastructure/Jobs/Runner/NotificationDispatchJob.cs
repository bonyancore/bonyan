using Construction.Platform.Core.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Sends queued email deliveries through the provider-neutral adapter (spec 01 §9). Every minute,
/// the job claims a batch with <c>FOR UPDATE SKIP LOCKED</c>; retry backoff is +1 m, +10 m, +1 h,
/// then terminal failed. Email is at-least-once.
/// </summary>
public sealed class NotificationDispatchJob(IServiceScopeFactory scopeFactory) : IJob
{
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public string ModuleCode => "platform";

    public string JobName => "platform_notification_dispatch";

    public string Queue => JobQueues.Default;

    public TimeSpan Timeout => TimeSpan.FromMinutes(2);

    public string? Cron => "* * * * *";

    public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        await dispatcher.DispatchAsync(BatchSize, cancellationToken).ConfigureAwait(false);
    }
}
