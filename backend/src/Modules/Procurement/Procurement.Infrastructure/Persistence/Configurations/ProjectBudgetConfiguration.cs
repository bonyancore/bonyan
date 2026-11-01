using Construction.Procurement.Core.Domain.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> builder)
    {
        builder.ToTable("project_budgets", table => table.HasCheckConstraint(
            "ck_project_budgets_limit_amount", "limit_amount >= 0 and reserved_amount >= 0 and reserved_amount <= limit_amount"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId }).IsUnique();
        builder.Property(item => item.LimitAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.ReservedAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Ignore(item => item.Limit);
        builder.Ignore(item => item.Remaining);
        builder.ConfigureAudit();
    }
}
