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
