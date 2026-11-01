using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Docs.Core.Application.Abstractions;
using Construction.Docs.Core.Domain.Documents;

namespace Construction.Docs.Core.Application;

public sealed class DocumentService(IDocumentRepository repository, IFileMetadataProvider files)
{
    public async Task<DocumentRecord> RegisterAsync(TenantId tenantId, string recordType,
        Guid linkedRecordId, Guid fileId, string title, string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (!await files.ExistsAsync(fileId, tenantId, cancellationToken))
            throw new DomainRuleViolation("docs.file_reference_not_found");
        var document = DocumentRecord.Create(Ids.New(), tenantId, recordType,
            linkedRecordId, fileId, title, description);
        await repository.AddAsync(document, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<DocumentVersion> AddVersionAsync(Guid documentId, TenantId tenantId,
        Guid fileId, string? label = null, CancellationToken cancellationToken = default)
    {
        if (!await files.ExistsAsync(fileId, tenantId, cancellationToken))
            throw new DomainRuleViolation("docs.file_reference_not_found");
        var document = await repository.GetAsync(documentId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("docs.document_not_found");
        var version = document.AddVersion(fileId, label, DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task ApproveVersionAsync(Guid documentId, int versionNumber, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(documentId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("docs.document_not_found");
        document.ApproveVersion(versionNumber, DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
