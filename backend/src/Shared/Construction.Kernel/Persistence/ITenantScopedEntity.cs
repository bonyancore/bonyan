using Construction.Kernel.Tenancy;

namespace Construction.Kernel.Persistence;

/// <summary>
/// Marks an entity as scoped to a specific tenant.
/// Automatically filtered by global query filters and guarded on persistence.
/// </summary>
public interface ITenantScopedEntity
{
    TenantId TenantId { get; set; }
}

