using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Docs.Core.Domain.Documents;

public sealed class DocumentRecord : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<DocumentVersion> _versions = [];
    private DocumentRecord() { }

    private DocumentRecord(Guid id, TenantId tenantId, string recordType, Guid linkedRecordId,
        Guid fileId, string title, string? description)
    {
        Id = id;
        TenantId = tenantId;
        RecordType = recordType;
        LinkedRecordId = linkedRecordId;
        Title = title;
        Description = description;
        AddVersion(fileId, null, DateTimeOffset.UtcNow);
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public string RecordType { get; private set; } = string.Empty;
    public Guid LinkedRecordId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IReadOnlyList<DocumentVersion> Versions => _versions.AsReadOnly();
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }

    public static DocumentRecord Create(Guid id, TenantId tenantId, string recordType,
        Guid linkedRecordId, Guid fileId, string title, string? description = null)
    {
        if (id == Guid.Empty || linkedRecordId == Guid.Empty || fileId == Guid.Empty)
            throw new DomainRuleViolation("docs.document_identity_required");
        if (!tenantId.IsValid)
            throw new DomainRuleViolation("docs.document_tenant_required");
        if (string.IsNullOrWhiteSpace(recordType) || recordType.Length > 100 ||
            recordType.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
            throw new DomainRuleViolation("docs.document_record_type_invalid");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300)
            throw new DomainRuleViolation("docs.document_title_required");
        if (description?.Trim().Length > 2000)
            throw new DomainRuleViolation("docs.document_description_too_long");
        return new DocumentRecord(id, tenantId, recordType.Trim().ToLowerInvariant(), linkedRecordId,
            fileId, title.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }

    public DocumentVersion AddVersion(Guid fileId, string? versionLabel, DateTimeOffset createdAt)
    {
        if (fileId == Guid.Empty || createdAt == default)
            throw new DomainRuleViolation("docs.document_version_details_required");
        if (versionLabel?.Length > 200)
            throw new DomainRuleViolation("docs.document_version_label_too_long");
        var version = new DocumentVersion(Ids.New(), Id, TenantId, _versions.Count + 1,
            fileId, versionLabel, createdAt);
        _versions.Add(version);
        return version;
    }

    public void ApproveVersion(int versionNumber, DateTimeOffset approvedAt)
    {
        var version = _versions.SingleOrDefault(item => item.VersionNumber == versionNumber)
            ?? throw new DomainRuleViolation("docs.document_version_not_found");
        if (version.Status == DocumentVersionStatus.Approved)
            return;
        if (version.Status == DocumentVersionStatus.Superseded)
            throw new DomainRuleViolation("docs.document_version_cannot_be_approved");
        foreach (var previouslyApproved in _versions.Where(item => item.Status == DocumentVersionStatus.Approved))
            previouslyApproved.MarkSuperseded();
        version.MarkApproved(approvedAt);
    }
}

public sealed class DocumentVersion : ITenantScopedEntity, IAuditableEntity
{
    private DocumentVersion() { }

    internal DocumentVersion(Guid id, Guid documentRecordId, TenantId tenantId, int versionNumber,
        Guid fileId, string? label, DateTimeOffset createdAt)
    {
        Id = id;
        DocumentRecordId = documentRecordId;
        TenantId = tenantId;
        VersionNumber = versionNumber;
        FileId = fileId;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        CreatedAt = createdAt.UtcDateTime;
        Status = DocumentVersionStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid DocumentRecordId { get; private set; }
    public TenantId TenantId { get; set; }
    public int VersionNumber { get; private set; }
    public Guid FileId { get; private set; }
    public string? Label { get; private set; }
    public DocumentVersionStatus Status { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void MarkApproved(DateTimeOffset approvedAt)
    {
        if (Status == DocumentVersionStatus.Superseded || approvedAt == default)
            throw new DomainRuleViolation("docs.document_version_cannot_be_approved");
        Status = DocumentVersionStatus.Approved;
        ApprovedAt = approvedAt.UtcDateTime;
    }

    internal void MarkSuperseded()
    {
        Status = DocumentVersionStatus.Superseded;
    }
}
