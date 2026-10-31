using Construction.Kernel;
using Construction.Kernel.Tenancy;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

public sealed record JobContext(
    TenantId TenantId,
    Guid RunId,
    IClock Clock,
    string? TimeZoneId = null);

/// <summary>
/// Background job registered by a module. Runs under a tenant context with a hard timeout.
/// </summary>
public interface IJob
{
    string ModuleCode { get; }
    string JobName { get; }
    string Queue { get; }
    TimeSpan Timeout { get; }

    /// <summary>Optional cron (5-field) evaluated in the tenant's timezone. Null = on-demand / loop-driven only.</summary>
    string? Cron { get; }

    Task RunAsync(JobContext context, CancellationToken cancellationToken);
}
