using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Xunit;

namespace Construction.Procurement.Domain.Tests;

public class PurchaseRequestTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 2, 3, 4, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Create_WithValidValues_StartsAsDraftAndNormalizesCreatedAtToUtc()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();
        var tenantId = TenantId.New();
        var request = PurchaseRequest.Create(id, projectId, requestedBy, tenantId, CreatedAt);

        Assert.Equal(id, request.Id);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal(requestedBy, request.RequestedBy);
        Assert.Equal(tenantId, request.TenantId);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Equal(CreatedAt.UtcDateTime, request.CreatedAt);
        Assert.Empty(request.Items);
    }

    [Fact]
    public void Create_WithEmptyRequiredId_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseRequest.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), TenantId.New(), CreatedAt));

        Assert.Equal("procurement.purchase_request_id_required", exception.Code);
    }

    [Fact]
    public void Create_WithoutProject_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseRequest.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), TenantId.New(), CreatedAt));

        Assert.Equal("procurement.purchase_request_project_required", exception.Code);
    }

    [Fact]
    public void Create_WithoutRequester_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, TenantId.New(), CreatedAt));

        Assert.Equal("procurement.purchase_request_requester_required", exception.Code);
    }

    [Fact]
    public void Create_WithoutTenant_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default, CreatedAt));

        Assert.Equal("procurement.purchase_request_tenant_required", exception.Code);
    }

    [Fact]
    public void Create_WithoutCreationTime_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId.New(), default));

        Assert.Equal("procurement.purchase_request_created_at_required", exception.Code);
    }

    [Fact]
    public void AddItem_WhenDraft_AddsItem()
    {
        var request = CreateRequest();
        var item = CreateItem();

        request.AddItem(item);

        Assert.Single(request.Items);
        Assert.Same(item, request.Items[0]);
        Assert.Equal(request.Id, item.PurchaseRequestId);
        Assert.Equal(request.TenantId, item.TenantId);
    }

    [Fact]
    public void AddItem_WhenItemIsNull_ThrowsDomainRuleViolation()
    {
        var request = CreateRequest();

        var exception = Assert.Throws<DomainRuleViolation>(() => request.AddItem(null!));

        Assert.Equal("procurement.purchase_request_item_required", exception.Code);
    }

    [Fact]
    public void Submit_WithAtLeastOneItem_MovesToSubmitted()
    {
        var request = CreateRequestWithItem();

        request.Submit();

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void Submit_WithoutItems_ThrowsAndKeepsDraftStatus()
    {
        var request = CreateRequest();

        var exception = Assert.Throws<DomainRuleViolation>(request.Submit);

        Assert.Equal("procurement.purchase_request_items_required", exception.Code);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
    }

    [Fact]
    public void Approve_WhenSubmitted_MovesToApproved()
    {
        var request = CreateSubmittedRequest();

        request.Approve();

        Assert.Equal(PurchaseRequestStatus.Approved, request.Status);
    }

    [Fact]
    public void Reject_WhenSubmitted_MovesToRejected()
    {
        var request = CreateSubmittedRequest();

        request.Reject();

        Assert.Equal(PurchaseRequestStatus.Rejected, request.Status);
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Draft)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Approve_WhenNotSubmitted_ThrowsDomainRuleViolation(PurchaseRequestStatus status)
    {
        var request = CreateRequestWithStatus(status);

        var exception = Assert.Throws<DomainRuleViolation>(() => request.Approve());

        Assert.Equal("procurement.purchase_request_invalid_transition", exception.Code);
        Assert.Equal(status, request.Status);
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Draft)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Reject_WhenNotSubmitted_ThrowsDomainRuleViolation(PurchaseRequestStatus status)
    {
        var request = CreateRequestWithStatus(status);

        var exception = Assert.Throws<DomainRuleViolation>(() => request.Reject());

        Assert.Equal("procurement.purchase_request_invalid_transition", exception.Code);
        Assert.Equal(status, request.Status);
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Draft)]
    [InlineData(PurchaseRequestStatus.Submitted)]
    public void Cancel_WhenDraftOrSubmitted_MovesToCancelled(PurchaseRequestStatus status)
    {
        var request = CreateRequestWithStatus(status);

        request.Cancel();

        Assert.Equal(PurchaseRequestStatus.Cancelled, request.Status);
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Cancel_WhenApprovedRejectedOrCancelled_ThrowsDomainRuleViolation(PurchaseRequestStatus status)
    {
        var request = CreateRequestWithStatus(status);

        var exception = Assert.Throws<DomainRuleViolation>(request.Cancel);

        Assert.Equal("procurement.purchase_request_invalid_transition", exception.Code);
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void AddItem_AfterSubmission_ThrowsDomainRuleViolation()
    {
        var request = CreateSubmittedRequest();

        var exception = Assert.Throws<DomainRuleViolation>(() => request.AddItem(CreateItem()));

        Assert.Equal("procurement.purchase_request_invalid_transition", exception.Code);
        Assert.Single(request.Items);
    }

    private static PurchaseRequest CreateRequest() =>
        PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId.New(), CreatedAt);

    private static PurchaseRequest CreateRequestWithItem()
    {
        var request = CreateRequest();
        request.AddItem(CreateItem());
        return request;
    }

    private static PurchaseRequest CreateSubmittedRequest()
    {
        var request = CreateRequestWithItem();
        request.Submit();
        return request;
    }

    private static PurchaseRequest CreateRequestWithStatus(PurchaseRequestStatus status)
    {
        var request = status is PurchaseRequestStatus.Submitted or PurchaseRequestStatus.Approved or PurchaseRequestStatus.Rejected
            ? CreateSubmittedRequest()
            : CreateRequest();

        if (status == PurchaseRequestStatus.Approved)
            request.Approve();
        else if (status == PurchaseRequestStatus.Rejected)
            request.Reject();
        else if (status == PurchaseRequestStatus.Cancelled)
            request.Cancel();

        return request;
    }

    private static PurchaseRequestItem CreateItem() => new("Portland cement", new Quantity(20m, "BAG"));
}
