using Construction.Kernel.Tenancy;
using Construction.Docs.Core.Application.Abstractions;
using Construction.Docs.Core.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace Construction.Docs.Infrastructure.Persistence;

public sealed class DocsRepository(DocsDbContext dbContext) : IDocumentRepository
{
    public Task AddAsync(DocumentRecord document, CancellationToken cancellationToken = default) =>
        dbContext.Documents.AddAsync(document, cancellationToken).AsTask();

    public Task<DocumentRecord?> GetAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Documents.Include(document => document.Versions)
            .SingleOrDefaultAsync(document => document.Id == id && document.TenantId == tenantId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
