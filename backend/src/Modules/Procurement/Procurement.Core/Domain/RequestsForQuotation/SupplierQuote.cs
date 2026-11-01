using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.RequestsForQuotation;

public sealed class SupplierQuote : ITenantScopedEntity, IAuditableEntity
{
    private readonly List<SupplierQuoteLine> _lines = [];
    private SupplierQuote() { }

    internal SupplierQuote(Guid supplierId, string currency, IReadOnlyList<SupplierQuoteLine> lines, DateTimeOffset submittedAt)
    {
        if (supplierId == Guid.Empty || string.IsNullOrWhiteSpace(currency) || submittedAt == default)
            throw new DomainRuleViolation("procurement.quote_details_invalid");
        Id = Ids.New();
        SupplierId = supplierId;
        Currency = currency;
        SubmittedAt = submittedAt.UtcDateTime;
        foreach (var line in lines)
        {
            line.Attach(Id, default);
            _lines.Add(line);
        }
    }

    public Guid Id { get; private set; }
    public Guid RequestForQuotationId { get; private set; }
    public Guid SupplierId { get; private set; }
    public TenantId TenantId { get; set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime SubmittedAt { get; private set; }
    public IReadOnlyList<SupplierQuoteLine> Lines => _lines.AsReadOnly();
    public decimal TotalAmount => _lines.Sum(line => line.LineTotal.Amount);
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void Attach(Guid rfqId, TenantId tenantId)
    {
        if (rfqId == Guid.Empty || !tenantId.IsValid || RequestForQuotationId != Guid.Empty)
            throw new DomainRuleViolation("procurement.quote_attach_invalid");
        RequestForQuotationId = rfqId;
        TenantId = tenantId;
        foreach (var line in _lines)
            line.Attach(Id, tenantId);
    }
}
