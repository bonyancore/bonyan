using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Application.Abstractions;

public interface IPurchaseRequestNumberGenerator
{
    Task<string> GenerateAsync(TenantId tenantId, Guid projectId, CancellationToken cancellationToken = default);
}
