using Construction.Docs.Core.Domain.Documents;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Xunit;

namespace Construction.Docs.Domain.Tests;

public sealed class DocumentRecordTests
{
    [Fact]
    public void Create_LinksMetadataToAnyRecordAndCreatesFirstDraftVersion()
    {
        var tenant = TenantId.New();
        var linkedRecord = Guid.NewGuid();
        var document = DocumentRecord.Create(Guid.NewGuid(), tenant, "procurement.purchase-order",
            linkedRecord, Guid.NewGuid(), "  Contract  ", "  Signed contract  ");

        Assert.Equal("procurement.purchase-order", document.RecordType);
        Assert.Equal(linkedRecord, document.LinkedRecordId);
        Assert.Equal("Contract", document.Title);
        Assert.Equal("Signed contract", document.Description);
        Assert.Equal(1, Assert.Single(document.Versions).VersionNumber);
        Assert.Equal(DocumentVersionStatus.Draft, document.Versions[0].Status);
    }

    [Fact]
    public void ApproveVersion_MarksPreviousApprovedVersionAsSuperseded()
    {
        var document = DocumentRecord.Create(Guid.NewGuid(), TenantId.New(), "supplier", Guid.NewGuid(),
            Guid.NewGuid(), "Insurance certificate");
        document.ApproveVersion(1, DateTimeOffset.UtcNow);
        var second = document.AddVersion(Guid.NewGuid(), "Renewed", DateTimeOffset.UtcNow);

        document.ApproveVersion(2, DateTimeOffset.UtcNow);

        Assert.Equal(DocumentVersionStatus.Superseded, document.Versions[0].Status);
        Assert.Equal(DocumentVersionStatus.Approved, second.Status);
        Assert.NotNull(second.ApprovedAt);
    }

    [Fact]
    public void ApproveVersion_CannotReapproveSupersededVersionOrRemoveCurrentApproval()
    {
        var document = DocumentRecord.Create(Guid.NewGuid(), TenantId.New(), "supplier", Guid.NewGuid(),
            Guid.NewGuid(), "Insurance certificate");
        document.ApproveVersion(1, DateTimeOffset.UtcNow);
        document.AddVersion(Guid.NewGuid(), "Renewed", DateTimeOffset.UtcNow);
        document.ApproveVersion(2, DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleViolation>(() => document.ApproveVersion(1, DateTimeOffset.UtcNow));

        Assert.Equal("docs.document_version_cannot_be_approved", exception.Code);
        Assert.Equal(DocumentVersionStatus.Superseded, document.Versions[0].Status);
        Assert.Equal(DocumentVersionStatus.Approved, document.Versions[1].Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not valid type")]
    public void Create_RejectsInvalidRecordType(string recordType)
    {
        var exception = Assert.Throws<DomainRuleViolation>(() => DocumentRecord.Create(
            Guid.NewGuid(), TenantId.New(), recordType, Guid.NewGuid(), Guid.NewGuid(), "Title"));

        Assert.Equal("docs.document_record_type_invalid", exception.Code);
    }
}
