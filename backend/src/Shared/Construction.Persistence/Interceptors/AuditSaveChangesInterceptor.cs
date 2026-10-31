using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Kernel.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Construction.Persistence.Interceptors;

/// <summary>
/// Intercepts SaveChanges / SaveChangesAsync to automatically stamp audit fields
/// and enforce tenant isolation, assignment, and immutability.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ITenantContext tenantContext,
    IUserContext userContext,
    IClock clock) : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IUserContext _userContext = userContext;
    private readonly IClock _clock = clock;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditRules(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditRules(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            HandleEntry(entry);
        }
    }

    private void HandleEntry(EntityEntry entry)
    {
        var now = _clock.UtcNow;

        // Fail-fast IUserContext enforcement:
        // 1. If IsAuthenticated is true, UserId MUST be non-null. Throw immediately if violated.
        // 2. If IsAuthenticated is false, treat as unauthenticated (null) regardless of any returned UserId.
        Guid? userId;
        if (_userContext.IsAuthenticated)
        {
            if (!_userContext.UserId.HasValue)
            {
                throw new InvalidOperationException(
                    "IUserContext contract violation: IsAuthenticated is true but UserId is null.");
            }

            userId = _userContext.UserId.Value;
        }
        else
        {
            userId = null;
        }

        switch (entry.State)
        {
            case EntityState.Added:
                HandleAdded(entry, now, userId);
                break;

            case EntityState.Modified:
                HandleModified(entry, now, userId);
                break;

            case EntityState.Deleted:
                HandleDeleted(entry);
                break;
        }
    }

    private void HandleAdded(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        // Audit stamping
        if (entry.Entity is IAuditableEntity auditable)
        {
            auditable.CreatedAt = now.UtcDateTime;
            auditable.CreatedBy = userId;
            auditable.UpdatedAt = now.UtcDateTime;
            auditable.UpdatedBy = userId;
        }

        // Tenant enforcement: Explicit validation at write boundary
        if (entry.Entity is ITenantScopedEntity tenantScoped)
        {
            if (!_tenantContext.HasTenant)
            {
                throw new InvalidOperationException(
                    $"Cannot add tenant-scoped entity '{entry.Metadata.DisplayName()}' " +
                    "without an active tenant context.");
            }

            var activeTenant = _tenantContext.Current;
            if (!activeTenant.IsValid)
            {
                throw new InvalidOperationException(
                    $"Cannot add tenant-scoped entity '{entry.Metadata.DisplayName()}': " +
                    "Active tenant context returned an invalid TenantId (default/empty).");
            }

            // Always overwrite — the valid active tenant is authoritative
            tenantScoped.TenantId = activeTenant;
        }
    }

    private void HandleModified(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        // Audit stamping — lock CreatedAt/CreatedBy against mutation
        if (entry.Entity is IAuditableEntity auditable)
        {
            auditable.UpdatedAt = now.UtcDateTime;
            auditable.UpdatedBy = userId;
            entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
            entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
        }

        // Tenant enforcement & mutation detection
        if (entry.Entity is ITenantScopedEntity tenantScoped)
        {
            if (!_tenantContext.HasTenant)
            {
                throw new InvalidOperationException(
                    $"Cannot modify tenant-scoped entity '{entry.Metadata.DisplayName()}' " +
                    "without an active tenant context.");
            }

            var activeTenant = _tenantContext.Current;
            if (!activeTenant.IsValid)
            {
                throw new InvalidOperationException(
                    $"Cannot modify tenant-scoped entity '{entry.Metadata.DisplayName()}': " +
                    "Active tenant context returned an invalid TenantId (default/empty).");
            }

            // Validate entity's own TenantId
            if (!tenantScoped.TenantId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Cannot modify tenant-scoped entity '{entry.Metadata.DisplayName()}': " +
                    "Entity has an invalid TenantId (default/empty).");
            }

            var tenantProp = entry.Property(nameof(ITenantScopedEntity.TenantId));

            // Detect attempted TenantId mutation on tracked entities
            // Compare OriginalValue and CurrentValue
            if (tenantProp.OriginalValue is not null && tenantProp.CurrentValue is not null)
            {
                var originalTenant = (TenantId)tenantProp.OriginalValue;
                var currentTenant = (TenantId)tenantProp.CurrentValue;

                if (!originalTenant.Equals(currentTenant))
                {
                    throw new InvalidOperationException(
                        $"TenantId mutation rejected on entity '{entry.Metadata.DisplayName()}'. " +
                        $"Attempted to change TenantId from '{originalTenant.Value}' to '{currentTenant.Value}'. " +
                        "TenantId is immutable.");
                }
            }

            // Entity must belong to the active tenant
            if (!tenantScoped.TenantId.Equals(activeTenant))
            {
                throw new InvalidOperationException(
                    $"Tenant mismatch: Cannot modify entity '{entry.Metadata.DisplayName()}' " +
                    $"belonging to tenant '{tenantScoped.TenantId.Value}' " +
                    $"under active tenant '{activeTenant.Value}'.");
            }

            // Lock TenantId against mutation in generated SQL
            tenantProp.IsModified = false;
        }
    }

    private void HandleDeleted(EntityEntry entry)
    {
        // Tenant enforcement — active tenant mandatory for hard deletes
        if (entry.Entity is ITenantScopedEntity tenantScoped)
        {
            if (!_tenantContext.HasTenant)
            {
                throw new InvalidOperationException(
                    $"Cannot delete tenant-scoped entity '{entry.Metadata.DisplayName()}' " +
                    "without an active tenant context.");
            }

            var activeTenant = _tenantContext.Current;
            if (!activeTenant.IsValid)
            {
                throw new InvalidOperationException(
                    $"Cannot delete tenant-scoped entity '{entry.Metadata.DisplayName()}': " +
                    "Active tenant context returned an invalid TenantId (default/empty).");
            }

            // Validate entity's own TenantId
            if (!tenantScoped.TenantId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Cannot delete tenant-scoped entity '{entry.Metadata.DisplayName()}': " +
                    "Entity has an invalid TenantId (default/empty).");
            }

            if (!tenantScoped.TenantId.Equals(activeTenant))
            {
                throw new InvalidOperationException(
                    $"Tenant mismatch: Cannot delete entity '{entry.Metadata.DisplayName()}' " +
                    $"belonging to tenant '{tenantScoped.TenantId.Value}' " +
                    $"under active tenant '{activeTenant.Value}'.");
            }
        }
    }
}
