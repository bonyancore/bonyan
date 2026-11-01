using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Contracts.Integration;
using Construction.Procurement.Core.Application;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Xunit;

namespace Construction.Procurement.Domain.Tests;

public sealed class PurchaseRequestWorkflowTests
{
    [Fact]
    public async Task CreateAndSubmit_UsesNumberingAndApprovalPorts()
    {
        var repository = new FakePurchaseRequestRepository();
        var approvals = new FakeApprovalEngine(new ApprovalDecision(true, "PLT208-DEV-000001"));
        var workflow = new PurchaseRequestWorkflow(repository, new FakeNumberGenerator(), approvals);
        var tenant = TenantId.New();
        var projectId = Guid.NewGuid();

        var request = await workflow.CreateAsync(projectId, Guid.NewGuid(), tenant,
            [("Cement", new Quantity(4m, "BAG"))]);
        var result = await workflow.SubmitAndRequestApprovalAsync(request.Id, tenant);

        Assert.Equal("PR-DEV-000001", result.RequestNumber);
        Assert.Equal(PurchaseRequestStatus.Approved, result.Status);
        Assert.Equal("PLT208-DEV-000001", result.ApprovalReference);
        Assert.Equal(1, approvals.Calls);
        Assert.Single(repository.Requests);
    }

    [Fact]
    public async Task SubmitAndRequestApproval_CanRetryAfterApprovalServiceFailure()
    {
        var repository = new FakePurchaseRequestRepository();
        var approvals = new FakeApprovalEngine(new ApprovalDecision(true, "approval-2")) { FailFirstCall = true };
        var workflow = new PurchaseRequestWorkflow(repository, new FakeNumberGenerator(), approvals);
        var tenant = TenantId.New();
        var request = PurchaseRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenant, DateTimeOffset.UtcNow);
        request.AddItem(new PurchaseRequestItem("Sand", new Quantity(2m, "M3")));
        await repository.AddAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SubmitAndRequestApprovalAsync(request.Id, tenant));
        var approved = await workflow.SubmitAndRequestApprovalAsync(request.Id, tenant);

        Assert.Equal(PurchaseRequestStatus.Approved, approved.Status);
        Assert.Equal(2, approvals.Calls);
    }

    [Fact]
    public async Task SiteConfirmation_CreatesOneDraftWithWorkPackageSnapshotWhenReplayed()
    {
        var repository = new FakePurchaseRequestRepository();
        var handler = new SitePurchaseRequestConfirmationHandler(repository, new FakeNumberGenerator());
        var message = new SitePurchaseRequestConfirmedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "WP-17", "Roof repairs",
            [new SitePurchaseRequestLineV1("Membrane", 15m, "M2")], DateTimeOffset.UtcNow);

        var first = await handler.HandleAsync(message);
        var replay = await handler.HandleAsync(message);

        Assert.Same(first, replay);
        Assert.Single(repository.Requests);
        Assert.Equal(PurchaseRequestStatus.Draft, first.Status);
        Assert.Equal("WP-17", first.WorkPackageCode);
        Assert.Equal("Roof repairs", first.WorkPackageName);
        Assert.Equal("PR-DEV-000001", first.RequestNumber);
        Assert.Single(first.Items);
    }

    private sealed class FakeNumberGenerator : IPurchaseRequestNumberGenerator
    {
        public Task<string> GenerateAsync(TenantId tenantId, Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult("PR-DEV-000001");
    }

    private sealed class FakeApprovalEngine(ApprovalDecision decision) : IApprovalEngine
    {
        public int Calls { get; private set; }
        public bool FailFirstCall { get; init; }

        public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (FailFirstCall && Calls == 1)
                throw new InvalidOperationException("mock approval unavailable");
            return Task.FromResult(decision);
        }
    }

    private sealed class FakePurchaseRequestRepository : IPurchaseRequestRepository
    {
        public List<PurchaseRequest> Requests { get; } = [];

        public Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<PurchaseRequest?> GetAsync(Guid id, TenantId tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Requests.SingleOrDefault(request => request.Id == id && request.TenantId == tenantId));

        public Task<PurchaseRequest?> FindBySourceEventAsync(Guid eventId, TenantId tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Requests.SingleOrDefault(request => request.SourceEventId == eventId && request.TenantId == tenantId));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
