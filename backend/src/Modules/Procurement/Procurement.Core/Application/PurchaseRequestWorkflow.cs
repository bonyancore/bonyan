using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.PurchaseRequests;

namespace Construction.Procurement.Core.Application;

public sealed class PurchaseRequestWorkflow(
    IPurchaseRequestRepository repository,
    IPurchaseRequestNumberGenerator numberGenerator,
    IApprovalEngine approvalEngine)
{
    public async Task<PurchaseRequest> CreateAsync(
        Guid projectId,
        Guid requestedBy,
        TenantId tenantId,
        IEnumerable<(string Description, Quantity Quantity)> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var request = PurchaseRequest.Create(Ids.New(), projectId, requestedBy, tenantId, DateTimeOffset.UtcNow);
        request.AssignRequestNumber(await numberGenerator.GenerateAsync(tenantId, projectId, cancellationToken));
        foreach (var (description, quantity) in items)
            request.AddItem(new PurchaseRequestItem(description, quantity));

        await repository.AddAsync(request, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PurchaseRequest> SubmitAndRequestApprovalAsync(
        Guid requestId,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = await repository.GetAsync(requestId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.purchase_request_not_found");
        if (request.Status == PurchaseRequestStatus.Draft)
        {
            request.Submit();
            await repository.SaveChangesAsync(cancellationToken);
        }
        else if (request.Status != PurchaseRequestStatus.Submitted)
        {
            throw new DomainRuleViolation("procurement.purchase_request_invalid_transition");
        }

        var decision = await approvalEngine.RequestApprovalAsync(
            new ApprovalRequest(
                request.Id,
                "purchase-request",
                request.RequestNumber ?? request.Id.ToString("N"),
                request.ProjectId,
                request.TenantId),
            cancellationToken);

        if (decision.IsApproved)
            request.Approve(decision.Reference);
        else
            request.Reject(decision.RejectionReason ?? decision.Reference);

        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }
}
