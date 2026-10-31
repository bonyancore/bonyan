using System.Linq.Expressions;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Construction.Persistence;

public static class GlobalQueryFilterExtensions
{
    public static void ApplyGlobalQueryFilters(this ModelBuilder modelBuilder, ModuleDbContext context)
    {
        // Phase 1: Validate inheritance rules (detect illegal derived-only implementations)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType != null)
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType) &&
                    !typeof(ISoftDeletable).IsAssignableFrom(entityType.BaseType.ClrType))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityType.ClrType.Name}' implements ISoftDeletable but its " +
                        $"base entity type '{entityType.BaseType.ClrType.Name}' does not. " +
                        $"ISoftDeletable must be declared on the root of an inheritance hierarchy.");
                }

                if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType) &&
                    !typeof(ITenantScopedEntity).IsAssignableFrom(entityType.BaseType.ClrType))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityType.ClrType.Name}' implements ITenantScopedEntity but its " +
                        $"base entity type '{entityType.BaseType.ClrType.Name}' does not. " +
                        $"ITenantScopedEntity must be declared on the root of an inheritance hierarchy.");
                }
            }
        }

        // Phase 2: Apply composed filters to root entity types
        var contextType = context.GetType();
        var currentTenantProp = Expression.Property(
            Expression.Constant(context, contextType),
            nameof(ModuleDbContext.CurrentTenantFilterId));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType != null)
            {
                continue;
            }

            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScopedEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable)
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "e");
            Expression? combinedBody = null;

            // 1. Incorporate existing custom query filter if present
#pragma warning disable CS0618 // Type or member is obsolete in EF Core 10
            var existingFilter = entityType.GetQueryFilter();
#pragma warning restore CS0618
            if (existingFilter != null)
            {
                combinedBody = ReplacingExpressionVisitor.Replace(
                    existingFilter.Parameters[0],
                    parameter,
                    existingFilter.Body);
            }

            // 2. Compose Tenant filter: (TenantId?)e.TenantId == context.CurrentTenantFilterId
            if (isTenantScoped)
            {
                var entityTenantProp = Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId));
                var entityNullableTenant = Expression.Convert(entityTenantProp, typeof(TenantId?));
                var tenantFilter = Expression.Equal(entityNullableTenant, currentTenantProp);

                combinedBody = combinedBody == null
                    ? tenantFilter
                    : Expression.AndAlso(combinedBody, tenantFilter);
            }

            // 3. Compose Soft-Delete filter: !e.IsDeleted
            if (isSoftDeletable)
            {
                var isDeletedProp = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var notDeletedFilter = Expression.Equal(isDeletedProp, Expression.Constant(false));

                combinedBody = combinedBody == null
                    ? notDeletedFilter
                    : Expression.AndAlso(combinedBody, notDeletedFilter);
            }

            if (combinedBody != null)
            {
                var lambda = Expression.Lambda(combinedBody, parameter);
                entityType.SetQueryFilter(lambda);
            }
        }
    }
}
