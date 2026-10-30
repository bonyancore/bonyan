namespace Construction.Kernel.Tenancy;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Ids.New());
    public override string ToString() => Value.ToString();
}

/// <summary>Tenant is resolved from the token claim "tid", never from a client header.</summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>Throws if there is no tenant.</summary>
    TenantId Current { get; }
}

public interface ITenantSetter
{
    void SetTenant(TenantId tenantId);
    void ClearTenant();
    IDisposable BeginScope(TenantId tenantId);
}

public sealed class AsyncLocalTenantContext : ITenantContext, ITenantSetter
{
    private static readonly AsyncLocal<TenantId?> CurrentTenant = new();

    public bool HasTenant => CurrentTenant.Value.HasValue;

    public TenantId Current => CurrentTenant.Value
        ?? throw new InvalidOperationException("No tenant context is currently set.");

    public void SetTenant(TenantId tenantId) => CurrentTenant.Value = tenantId;

    public void ClearTenant() => CurrentTenant.Value = null;

    public IDisposable BeginScope(TenantId tenantId)
    {
        var previous = CurrentTenant.Value;
        CurrentTenant.Value = tenantId;
        return new Scope(() => CurrentTenant.Value = previous);
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
