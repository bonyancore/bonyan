using Construction.Kernel.Tenancy;

namespace Construction.Docs.Core.Application.Abstractions;

public interface IFileMetadataProvider
{
    Task<bool> ExistsAsync(Guid fileId, TenantId tenantId, CancellationToken cancellationToken = default);
}
