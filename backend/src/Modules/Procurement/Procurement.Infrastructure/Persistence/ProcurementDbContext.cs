using Construction.Kernel.Tenancy;
using Construction.Persistence;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.Outbox;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Construction.Procurement.Core.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Construction.Procurement.Infrastructure.Persistence;

public sealed class ProcurementDbContext(
    DbContextOptions<ProcurementDbContext> options,
    ITenantContext tenantContext)
    : ModuleDbContext(options, Schema, tenantContext)
{
    public const string Schema = "procurement";

    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();

    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<RequestForQuotation> RequestsForQuotation => Set<RequestForQuotation>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();

    public DbSet<ProcurementOutboxMessage> OutboxMessages => Set<ProcurementOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcurementDbContext).Assembly);
    }
}
