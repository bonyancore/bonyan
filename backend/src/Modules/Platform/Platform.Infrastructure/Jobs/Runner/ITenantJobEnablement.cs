namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>Per-tenant flag: whether a job is allowed to run for that tenant.</summary>
public interface ITenantJobEnablement
{
    bool IsEnabled(Guid tenantId, string jobName);
}

/// <summary>Default: all jobs enabled unless explicitly disabled in the dictionary.</summary>
public sealed class DictionaryTenantJobEnablement : ITenantJobEnablement
{
    private readonly HashSet<(Guid TenantId, string JobName)> _disabled;

    public DictionaryTenantJobEnablement(IEnumerable<(Guid TenantId, string JobName)>? disabled = null)
    {
        _disabled = new HashSet<(Guid, string)>(disabled ?? []);
    }

    public bool IsEnabled(Guid tenantId, string jobName) =>
        !_disabled.Contains((tenantId, jobName));
}
