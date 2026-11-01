using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.PurchaseOrders;

namespace Construction.Procurement.Core.Application;

public sealed class ProjectBudgetWorkflow(
    IProjectBudgetProvider budgets,
    IProjectBudgetAdminRepository repository)
{
    public async Task<ProjectBudget> SetLimitAsync(Guid projectId, TenantId tenantId, Money limit,
        CancellationToken cancellationToken = default)
    {
        var existing = await budgets.GetBudgetAsync(projectId, tenantId, cancellationToken);
        if (existing is null)
        {
            var budget = ProjectBudget.Create(Ids.New(), tenantId, projectId, limit);
            await repository.AddAsync(budget, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return budget;
        }

        existing.UpdateLimit(limit);
        await repository.SaveChangesAsync(cancellationToken);
        return existing;
    }
}
