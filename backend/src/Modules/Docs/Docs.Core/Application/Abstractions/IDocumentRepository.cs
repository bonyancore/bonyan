using Construction.Kernel.Tenancy;
using Construction.Docs.Core.Domain.Documents;

namespace Construction.Docs.Core.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(DocumentRecord document, CancellationToken cancellationToken = default);
    Task<DocumentRecord?> GetAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
