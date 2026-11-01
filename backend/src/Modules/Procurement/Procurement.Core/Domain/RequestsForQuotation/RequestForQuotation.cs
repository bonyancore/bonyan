using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Domain.PurchaseRequests;

namespace Construction.Procurement.Core.Domain.RequestsForQuotation;

public sealed class RequestForQuotation : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private readonly List<RequestForQuotationInvitation> _invitations = [];
    private readonly List<SupplierQuote> _quotes = [];

    private RequestForQuotation() { }

    private RequestForQuotation(Guid id, TenantId tenantId, Guid purchaseRequestId)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseRequestId = purchaseRequestId;
        Status = RequestForQuotationStatus.Draft;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; set; }

    public Guid PurchaseRequestId { get; private set; }

    public RequestForQuotationStatus Status { get; private set; }

    public Guid? AwardedSupplierId { get; private set; }

    public IReadOnlyList<Guid> InvitedSupplierIds => _invitations.Select(item => item.SupplierId).ToArray();

    public IReadOnlyList<SupplierQuote> Quotes => _quotes.AsReadOnly();

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public uint RowVersion { get; set; }

    public static RequestForQuotation Create(Guid id, TenantId tenantId, PurchaseRequest purchaseRequest)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);

        if (id == Guid.Empty || purchaseRequest.Id == Guid.Empty)
            throw new DomainRuleViolation("procurement.rfq_id_required");

        if (!tenantId.IsValid || tenantId != purchaseRequest.TenantId)
            throw new DomainRuleViolation("procurement.rfq_tenant_mismatch");

        if (purchaseRequest.Status != PurchaseRequestStatus.Approved)
            throw new DomainRuleViolation("procurement.rfq_purchase_request_not_approved");

        if (purchaseRequest.Items.Count == 0)
            throw new DomainRuleViolation("procurement.rfq_purchase_request_items_required");

        return new RequestForQuotation(id, tenantId, purchaseRequest.Id);
    }

    public void InviteSupplier(Guid supplierId)
    {
        EnsureStatus(RequestForQuotationStatus.Draft);

        if (supplierId == Guid.Empty)
            throw new DomainRuleViolation("procurement.rfq_supplier_required");

        if (_invitations.Any(item => item.SupplierId == supplierId))
            throw new DomainRuleViolation("procurement.rfq_supplier_already_invited");

        var invitation = new RequestForQuotationInvitation(supplierId);
        invitation.Attach(Id, TenantId);
        _invitations.Add(invitation);
    }

    public void Issue()
    {
        EnsureStatus(RequestForQuotationStatus.Draft);

        if (_invitations.Count == 0)
            throw new DomainRuleViolation("procurement.rfq_suppliers_required");

        Status = RequestForQuotationStatus.Issued;
    }

    public void RecordQuote(
        Guid supplierId,
        IReadOnlyCollection<PurchaseRequestItem> expectedPurchaseRequestItems,
        IReadOnlyList<SupplierQuoteLine> lines,
        DateTimeOffset submittedAt)
    {
        EnsureStatus(RequestForQuotationStatus.Issued);
        ArgumentNullException.ThrowIfNull(expectedPurchaseRequestItems);
        ArgumentNullException.ThrowIfNull(lines);

        if (!_invitations.Any(item => item.SupplierId == supplierId))
            throw new DomainRuleViolation("procurement.rfq_supplier_not_invited");

        if (_quotes.Any(quote => quote.SupplierId == supplierId))
            throw new DomainRuleViolation("procurement.rfq_quote_already_recorded");

        if (lines.Count == 0 || lines.Select(line => line.PurchaseRequestItemId).Distinct().Count() != lines.Count)
            throw new DomainRuleViolation("procurement.rfq_quote_lines_invalid");

        if (!expectedPurchaseRequestItems.Select(item => item.Id).ToHashSet()
            .SetEquals(lines.Select(line => line.PurchaseRequestItemId)))
            throw new DomainRuleViolation("procurement.rfq_quote_lines_must_match_request");
        foreach (var line in lines)
        {
            var expected = expectedPurchaseRequestItems.Single(item => item.Id == line.PurchaseRequestItemId);
            if (line.Quantity != expected.Quantity)
                throw new DomainRuleViolation("procurement.rfq_quote_quantity_must_match_request");
        }

        var currency = lines[0].UnitPrice.Currency;
        if (lines.Any(line => line.UnitPrice.Currency != currency))
            throw new DomainRuleViolation("procurement.rfq_quote_currency_mismatch");

        var quote = new SupplierQuote(supplierId, currency, lines.ToArray(), submittedAt.ToUniversalTime());
        quote.Attach(Id, TenantId);
        _quotes.Add(quote);
    }

    public IReadOnlyList<SupplierQuoteComparison> CompareQuotes()
    {
        return _quotes
            .Select(quote => new SupplierQuoteComparison(
                quote.SupplierId,
                quote.Currency,
                quote.TotalAmount,
                quote.SubmittedAt,
                quote.Lines.Select(line => new SupplierQuoteComparisonLine(
                    line.PurchaseRequestItemId,
                    line.Description,
                    line.Quantity.Amount,
                    line.Quantity.Unit,
                    line.UnitPrice.Amount,
                    line.LineTotal.Amount)).ToArray()))
            .OrderBy(quote => quote.Currency, StringComparer.Ordinal)
            .ThenBy(quote => quote.TotalAmount)
            .ToArray();
    }

    public SupplierQuote AwardQuote(Guid supplierId)
    {
        EnsureStatus(RequestForQuotationStatus.Issued);
        var quote = _quotes.SingleOrDefault(item => item.SupplierId == supplierId)
            ?? throw new DomainRuleViolation("procurement.rfq_quote_not_found");

        AwardedSupplierId = supplierId;
        Status = RequestForQuotationStatus.Awarded;
        return quote;
    }

    public void Close()
    {
        if (Status is not (RequestForQuotationStatus.Issued or RequestForQuotationStatus.Awarded))
            throw new DomainRuleViolation("procurement.rfq_invalid_transition");

        Status = RequestForQuotationStatus.Closed;
    }

    public void Cancel()
    {
        if (Status is not (RequestForQuotationStatus.Draft or RequestForQuotationStatus.Issued))
            throw new DomainRuleViolation("procurement.rfq_invalid_transition");

        Status = RequestForQuotationStatus.Cancelled;
    }

    private void EnsureStatus(RequestForQuotationStatus expected)
    {
        if (Status != expected)
            throw new DomainRuleViolation("procurement.rfq_invalid_transition");
    }
}
