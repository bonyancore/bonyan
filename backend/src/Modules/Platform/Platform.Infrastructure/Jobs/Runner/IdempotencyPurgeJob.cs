using Construction.Web.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Purges expired idempotency keys (spec 01 §9 <c>platform_idempotency_purge</c>).
/// Runs hourly because the hand-written cron parser supports only <c>M H * * *</c>; the spec's
/// twice-hourly cadence needs the cron parser extension tracked in PLT-211.
/// </summary>
public sealed class IdempotencyPurgeJob(IServiceScopeFactory scopeFactory) : IJob
{
    public const int BatchSize = 10_000;

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public string ModuleCode => "platform";

    public string JobName => "platform_idempotency_purge";

    public string Queue => JobQueues.Default;

    public TimeSpan Timeout => TimeSpan.FromMinutes(2);

    public string? Cron => "0 * * * *";

    public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        await store.PurgeExpiredAsync(BatchSize, cancellationToken).ConfigureAwait(false);
    }
}
