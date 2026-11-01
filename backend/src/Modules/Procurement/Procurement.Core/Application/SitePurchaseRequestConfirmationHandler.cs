using Construction.Kernel;
using Construction.Procurement.Contracts.Integration;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.PurchaseRequests;

namespace Construction.Procurement.Core.Application;

/// <summary>Maps the temporary Core event contract into a local draft and is safe to replay.</summary>
public sealed class SitePurchaseRequestConfirmationHandler(
    IPurchaseRequestRepository repository,
    IPurchaseRequestNumberGenerator numberGenerator)
{
    public async Task<PurchaseRequest> HandleAsync(
        SitePurchaseRequestConfirmedV1 message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var tenantId = new Construction.Kernel.Tenancy.TenantId(message.TenantId);
        var existing = await repository.FindBySourceEventAsync(message.EventId, tenantId, cancellationToken);
        if (existing is not null)
            return existing;

        var request = PurchaseRequest.Create(
            Ids.New(), message.ProjectId, message.RequestedBy, tenantId, message.ConfirmedAt);
        request.AssignRequestNumber(await numberGenerator.GenerateAsync(tenantId, message.ProjectId, cancellationToken));
        request.SetSourceEvent(message.EventId);
        request.AttachWorkPackageSnapshot(message.WorkPackageId, message.WorkPackageCode, message.WorkPackageName);
        foreach (var line in message.Lines)
            request.AddItem(new PurchaseRequestItem(line.Description, new Quantity(line.Quantity, line.Unit)));

        await repository.AddAsync(request, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }
}
