using Construction.Kernel.Tenancy;

namespace Construction.Persistence.Tenancy;

public sealed class SettableTenantContext : ITenantContext, ITenantSetter
{
    private TenantId? _current;

    public bool HasTenant => _current.HasValue;

    public TenantId Current => _current ?? throw new InvalidOperationException("No tenant has been set for this scope.");

    public void SetTenant(TenantId tenantId)
    {
        tenantId.EnsureValid();

        if (_current.HasValue)
        {
            throw new InvalidOperationException("Tenant context has already been set for this scope and is immutable.");
        }

        _current = tenantId;
    }

    public IDisposable BeginScope(TenantId tenantId)
    {
        tenantId.EnsureValid();
        var previous = _current;
        _current = tenantId;
        return new Scope(() => _current = previous);
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

