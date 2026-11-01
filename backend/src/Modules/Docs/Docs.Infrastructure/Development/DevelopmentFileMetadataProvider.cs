using Construction.Kernel.Tenancy;
using Construction.Docs.Core.Application.Abstractions;

namespace Construction.Docs.Infrastructure.Development;

/// <summary>Temporary PLT-207 adapter for local development; replace when the file-service contract is ready.</summary>
public sealed class DevelopmentFileMetadataProvider : IFileMetadataProvider
{
    public Task<bool> ExistsAsync(Guid fileId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(fileId != Guid.Empty && tenantId.IsValid);
    }
}
