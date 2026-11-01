using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Domain.Suppliers;

public sealed class Supplier : ITenantScopedEntity, IAuditableEntity, IConcurrencyVersioned
{
    private Supplier() { }

    private Supplier(Guid id, TenantId tenantId, Guid corePartyId, string name, string? email, string? phone)
    {
        Id = id;
        TenantId = tenantId;
        CorePartyId = corePartyId;
        Name = name;
        Email = email;
        Phone = phone;
        Status = SupplierStatus.Active;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; set; }

    public Guid CorePartyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public SupplierStatus Status { get; private set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public uint RowVersion { get; set; }

    public static Supplier Create(
        Guid id,
        TenantId tenantId,
        Guid corePartyId,
        string name,
        string? email = null,
        string? phone = null)
    {
        if (id == Guid.Empty || corePartyId == Guid.Empty)
            throw new DomainRuleViolation("procurement.supplier_party_required");

        if (!tenantId.IsValid)
            throw new DomainRuleViolation("procurement.supplier_tenant_required");

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new DomainRuleViolation("procurement.supplier_name_required");
        if (email?.Trim().Length > 320 || phone?.Trim().Length > 40)
            throw new DomainRuleViolation("procurement.supplier_contact_too_long");

        return new Supplier(id, tenantId, corePartyId, name.Trim(), Normalize(email), Normalize(phone));
    }

    public void UpdateContact(string? email, string? phone)
    {
        if (email?.Trim().Length > 320 || phone?.Trim().Length > 40)
            throw new DomainRuleViolation("procurement.supplier_contact_too_long");
        Email = Normalize(email);
        Phone = Normalize(phone);
    }

    public void Deactivate() => Status = SupplierStatus.Inactive;

    public void Activate() => Status = SupplierStatus.Active;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
