using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.RequestsForQuotation;

namespace Construction.Procurement.Core.Application;

public sealed class RequestForQuotationWorkflow(
    IRequestForQuotationRepository repository,
    ISupplierRegistryRepository suppliers)
{
    public async Task<RequestForQuotation> CreateAsync(Guid requestId, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var request = await repository.GetApprovedRequestAsync(requestId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.approved_purchase_request_not_found");
        var rfq = RequestForQuotation.Create(Ids.New(), tenantId, request);
        await repository.AddAsync(rfq, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<RequestForQuotation> InviteSupplierAsync(Guid rfqId, Guid supplierId,
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (!await suppliers.SupplierExistsAsync(supplierId, tenantId, cancellationToken))
            throw new KeyNotFoundException("procurement.supplier_not_found");
        var rfq = await repository.GetAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        rfq.InviteSupplier(supplierId);
        await repository.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<RequestForQuotation> IssueAsync(Guid rfqId, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var rfq = await repository.GetAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        rfq.Issue();
        await repository.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<RequestForQuotation> RecordQuoteAsync(Guid rfqId, Guid supplierId,
        IReadOnlyList<SupplierQuoteLine> lines, DateTimeOffset submittedAt, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var rfq = await repository.GetAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        var request = await repository.GetApprovedRequestAsync(rfq.PurchaseRequestId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.purchase_request_not_found");
        rfq.RecordQuote(supplierId, request.Items, lines, submittedAt);
        await repository.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<RequestForQuotation> AwardAsync(Guid rfqId, Guid supplierId, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var rfq = await repository.GetAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        rfq.AwardQuote(supplierId);
        await repository.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<IReadOnlyList<SupplierQuoteComparison>> CompareAsync(Guid rfqId, TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var rfq = await repository.GetAsync(rfqId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.rfq_not_found");
        return rfq.CompareQuotes();
    }
}
