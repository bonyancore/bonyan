using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.PurchaseOrders;

public sealed class ProjectBudget : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private ProjectBudget() { }

    private ProjectBudget(Guid id, TenantId tenantId, Guid projectId, Money limit)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        LimitAmount = limit.Amount;
        Currency = limit.Currency;
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid ProjectId { get; private set; }
    public decimal LimitAmount { get; private set; }
    public decimal ReservedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Money Limit => new(LimitAmount, Currency);
    public Money Remaining => new(LimitAmount - ReservedAmount, Currency);
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }

    public static ProjectBudget Create(Guid id, TenantId tenantId, Guid projectId, Money limit)
    {
        if (id == Guid.Empty || projectId == Guid.Empty || !tenantId.IsValid || limit.Amount < 0)
            throw new DomainRuleViolation("procurement.project_budget_invalid");
        return new ProjectBudget(id, tenantId, projectId, limit);
    }

    public void UpdateLimit(Money limit)
    {
        if (limit.Amount < ReservedAmount || limit.Currency != Currency)
            throw new DomainRuleViolation("procurement.project_budget_invalid");
        LimitAmount = limit.Amount;
    }

    public void Reserve(Money amount)
    {
        if (amount.Amount < 0 || amount.Currency != Currency || ReservedAmount + amount.Amount > LimitAmount)
            throw new DomainRuleViolation("procurement.po_budget_exceeded");
        ReservedAmount += amount.Amount;
    }

    public void Release(Money amount)
    {
        if (amount.Amount < 0 || amount.Currency != Currency || amount.Amount > ReservedAmount)
            throw new DomainRuleViolation("procurement.project_budget_release_invalid");
        ReservedAmount -= amount.Amount;
    }
}
