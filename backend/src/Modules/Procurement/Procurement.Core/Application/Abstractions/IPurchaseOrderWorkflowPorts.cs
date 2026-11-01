using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Events;
using Construction.Procurement.Core.Domain.PurchaseOrders;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Construction.Procurement.Core.Domain.RequestsForQuotation;
using Construction.Procurement.Core.Domain.Receipts;
using Construction.Procurement.Core.Domain.Invoices;
using Construction.Procurement.Core.Domain.Suppliers;

namespace Construction.Procurement.Core.Application.Abstractions;

public interface IPurchaseOrderRepository
{
    Task<PurchaseRequest?> GetPurchaseRequestAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<RequestForQuotation?> GetRequestForQuotationAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddPurchaseOrderAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPurchaseOrderNumberGenerator
{
    Task<string> GenerateAsync(TenantId tenantId, Guid projectId, CancellationToken cancellationToken = default);
}

public interface IProjectBudgetProvider
{
    Task<ProjectBudget?> GetBudgetAsync(Guid projectId, TenantId tenantId, CancellationToken cancellationToken = default);
}

public interface IProjectBudgetAdminRepository
{
    Task AddAsync(ProjectBudget budget, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProcurementEventPublisher
{
    Task PublishAsync(IProcurementEvent @event, CancellationToken cancellationToken = default);
}

public interface ISupplierRegistryRepository
{
    Task<bool> CorePartyAlreadyRegisteredAsync(Guid corePartyId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> SupplierExistsAsync(Guid supplierId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid supplierId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRequestForQuotationRepository
{
    Task<PurchaseRequest?> GetApprovedRequestAsync(Guid requestId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<RequestForQuotation?> GetAsync(Guid rfqId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(RequestForQuotation rfq, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IGoodsReceiptRepository
{
    Task<PurchaseOrder?> GetOrderAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceipt>> GetReceiptsAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetReceiptAsync(Guid receiptId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ISupplierInvoiceRepository
{
    Task<PurchaseOrder?> GetOrderAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceipt>> GetReceiptsAsync(Guid orderId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<SupplierInvoice?> GetInvoiceAsync(Guid invoiceId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(SupplierInvoice invoice, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
