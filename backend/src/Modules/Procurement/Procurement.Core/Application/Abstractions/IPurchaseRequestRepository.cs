using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseRequests;

namespace Construction.Procurement.Core.Application.Abstractions;

public interface IPurchaseRequestRepository
{
    Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseRequest?> GetAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<PurchaseRequest?> FindBySourceEventAsync(Guid eventId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
