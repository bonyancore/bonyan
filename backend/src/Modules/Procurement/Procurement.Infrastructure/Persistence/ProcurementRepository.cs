using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.Outbox;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Construction.Procurement.Infrastructure.Persistence;

public sealed class ProcurementRepository(ProcurementDbContext dbContext) :
    IPurchaseRequestRepository, IPurchaseOrderRepository, IProjectBudgetProvider, IProcurementEventPublisher,
    ISupplierRegistryRepository, IRequestForQuotationRepository, IGoodsReceiptRepository, ISupplierInvoiceRepository,
    IProjectBudgetAdminRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseRequests.AddAsync(request, cancellationToken).AsTask();

    public Task<PurchaseRequest?> GetAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseRequests.Include(request => request.Items)
            .SingleOrDefaultAsync(request => request.Id == id && request.TenantId == tenantId, cancellationToken);

    public Task<PurchaseRequest?> FindBySourceEventAsync(Guid eventId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseRequests.Include(request => request.Items)
            .SingleOrDefaultAsync(request => request.SourceEventId == eventId && request.TenantId == tenantId, cancellationToken);

    public async Task<PurchaseRequest?> GetPurchaseRequestAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
        await GetAsync(id, tenantId, cancellationToken);

    public Task<bool> CorePartyAlreadyRegisteredAsync(Guid corePartyId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AnyAsync(supplier => supplier.CorePartyId == corePartyId && supplier.TenantId == tenantId, cancellationToken);

    public Task<bool> SupplierExistsAsync(Guid supplierId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AnyAsync(supplier => supplier.Id == supplierId && supplier.TenantId == tenantId, cancellationToken);

    public Task<Supplier?> GetSupplierAsync(Guid supplierId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.SingleOrDefaultAsync(supplier => supplier.Id == supplierId && supplier.TenantId == tenantId, cancellationToken);

    public Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AddAsync(supplier, cancellationToken).AsTask();

    public Task<RequestForQuotation?> GetRequestForQuotationAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.RequestsForQuotation.Include("_invitations").Include(rfq => rfq.Quotes).ThenInclude(quote => quote.Lines)
            .SingleOrDefaultAsync(rfq => rfq.Id == id && rfq.TenantId == tenantId, cancellationToken);

    public Task<PurchaseRequest?> GetApprovedRequestAsync(Guid requestId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseRequests.Include(request => request.Items)
            .SingleOrDefaultAsync(request => request.Id == requestId && request.TenantId == tenantId &&
                request.Status == PurchaseRequestStatus.Approved, cancellationToken);

    Task<RequestForQuotation?> IRequestForQuotationRepository.GetAsync(Guid rfqId, TenantId tenantId, CancellationToken cancellationToken) =>
        GetRequestForQuotationAsync(rfqId, tenantId, cancellationToken);

    public Task AddAsync(RequestForQuotation rfq, CancellationToken cancellationToken = default) =>
        dbContext.RequestsForQuotation.AddAsync(rfq, cancellationToken).AsTask();

    public Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders.Include(order => order.Lines)
            .Include(order => order.Revisions).ThenInclude(revision => revision.Lines)
            .SingleOrDefaultAsync(order => order.Id == id && order.TenantId == tenantId, cancellationToken);

    public Task<PurchaseOrder?> GetOrderAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        GetPurchaseOrderAsync(orderId, tenantId, cancellationToken);

    public async Task<IReadOnlyList<GoodsReceipt>> GetReceiptsAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.GoodsReceipts.Include(receipt => receipt.Lines)
            .Where(receipt => receipt.PurchaseOrderId == orderId && receipt.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public Task<GoodsReceipt?> GetReceiptAsync(Guid receiptId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.GoodsReceipts.Include(receipt => receipt.Lines)
            .SingleOrDefaultAsync(receipt => receipt.Id == receiptId && receipt.TenantId == tenantId, cancellationToken);

    public Task AddAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default) =>
        dbContext.GoodsReceipts.AddAsync(receipt, cancellationToken).AsTask();

    public Task<SupplierInvoice?> GetInvoiceAsync(Guid invoiceId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.SupplierInvoices.Include(invoice => invoice.Lines)
            .SingleOrDefaultAsync(invoice => invoice.Id == invoiceId && invoice.TenantId == tenantId, cancellationToken);

    public Task AddAsync(SupplierInvoice invoice, CancellationToken cancellationToken = default) =>
        dbContext.SupplierInvoices.AddAsync(invoice, cancellationToken).AsTask();

    public Task AddPurchaseOrderAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken).AsTask();

    public Task<ProjectBudget?> GetBudgetAsync(Guid projectId, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.ProjectBudgets.SingleOrDefaultAsync(
            item => item.ProjectId == projectId && item.TenantId == tenantId, cancellationToken);

    public async Task SetBudgetAsync(ProjectBudget budget, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ProjectBudgets.SingleOrDefaultAsync(item =>
            item.TenantId == budget.TenantId && item.ProjectId == budget.ProjectId, cancellationToken);
        if (existing is null)
            dbContext.ProjectBudgets.Add(budget);
        else
            existing.UpdateLimit(budget.Limit);
    }

    public Task AddAsync(ProjectBudget budget, CancellationToken cancellationToken = default) =>
        dbContext.ProjectBudgets.AddAsync(budget, cancellationToken).AsTask();

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    public Task PublishAsync(IProcurementEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var payload = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);
        var message = ProcurementOutboxMessage.Create(@event, @event.EventType, payload, @event.OccurredAt);
        return dbContext.OutboxMessages.AddAsync(message, cancellationToken).AsTask();
    }
}
