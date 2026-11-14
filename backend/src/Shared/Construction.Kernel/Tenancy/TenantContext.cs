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

public interface ITenantSetter
{
    void SetTenant(TenantId tenantId);
    IDisposable BeginScope(TenantId tenantId);
}

/// <summary>
/// AsyncLocal-backed implementation used in tests and in-process middleware.
/// Each async context (async/await branch) sees its own tenant, so tests can
/// run concurrently without sharing state.
/// </summary>
public sealed class AsyncLocalTenantContext : ITenantContext, ITenantSetter
{
    private static readonly AsyncLocal<TenantId?> CurrentTenant = new();

    public bool HasTenant => CurrentTenant.Value.HasValue;

    public TenantId Current => CurrentTenant.Value
        ?? throw new InvalidOperationException("No tenant context is currently set.");

    public void SetTenant(TenantId tenantId) => CurrentTenant.Value = tenantId;

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
