namespace Construction.Kernel.Tenancy;

public readonly record struct TenantId
{
    public Guid Value { get; init; }

    /// <summary>
    /// Indicates whether this TenantId represents a valid, non-empty tenant.
    /// Evaluates to false for default(TenantId) or Guid.Empty.
    /// </summary>
    public bool IsValid => Value != Guid.Empty;

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId cannot be empty. Guid.Empty (00000000-0000-0000-0000-000000000000) " +
                "is reserved as the system fail-closed sentinel.",
                nameof(value));
        }

        Value = value;
    }

    public static TenantId New() => new(Ids.New());

    public static TenantId From(Guid value) => new(value);

    public void Deconstruct(out Guid value) => value = Value;

    /// <summary>Throws an InvalidOperationException if the struct is in an uninitialized/default state.</summary>
    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("TenantId is not initialized (default value is invalid).");
        }
    }

    public override string ToString() => Value.ToString();
}

/// <summary>Tenant is resolved from the token claim "tid", never from a client header.</summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>Throws if there is no tenant.</summary>
    TenantId Current { get; }
}
