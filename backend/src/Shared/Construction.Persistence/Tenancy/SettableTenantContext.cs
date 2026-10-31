using Construction.Kernel.Tenancy;

namespace Construction.Persistence.Tenancy;

public interface ITenantSetter
{
    void SetTenant(TenantId tenantId);
}

public sealed class SettableTenantContext : ITenantContext, ITenantSetter
{
    private TenantId? _current;

    public bool HasTenant => _current.HasValue;

    public TenantId Current => _current ?? throw new InvalidOperationException("No tenant has been set for this scope.");

    void ITenantSetter.SetTenant(TenantId tenantId)
    {
        tenantId.EnsureValid();

        if (_current.HasValue)
        {
            throw new InvalidOperationException("Tenant context has already been set for this scope and is immutable.");
        }

        _current = tenantId;
    }
}

