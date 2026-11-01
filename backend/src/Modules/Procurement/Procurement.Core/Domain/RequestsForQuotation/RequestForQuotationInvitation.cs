using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.RequestsForQuotation;

public sealed class RequestForQuotationInvitation : ITenantScopedEntity, IAuditableEntity
{
    private RequestForQuotationInvitation() { }
    internal RequestForQuotationInvitation(Guid supplierId) => SupplierId = supplierId;

    public Guid Id { get; private set; } = Ids.New();
    public Guid RequestForQuotationId { get; private set; }
    public Guid SupplierId { get; private set; }
    public TenantId TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal void Attach(Guid rfqId, TenantId tenantId)
    {
        if (rfqId == Guid.Empty || !tenantId.IsValid || RequestForQuotationId != Guid.Empty)
            throw new DomainRuleViolation("procurement.rfq_invitation_attach_invalid");
        RequestForQuotationId = rfqId;
        TenantId = tenantId;
    }
}
