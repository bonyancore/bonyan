using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;
using Construction.Procurement.Core.Domain.Suppliers;

namespace Construction.Procurement.Core.Application;

public sealed class SupplierRegistry(ISupplierRegistryRepository repository)
{
    public async Task<Supplier> RegisterAsync(Guid corePartyId, TenantId tenantId, string name,
        string? email, string? phone, CancellationToken cancellationToken = default)
    {
        if (await repository.CorePartyAlreadyRegisteredAsync(corePartyId, tenantId, cancellationToken))
            throw new DomainRuleViolation("procurement.supplier_party_already_registered");
        var supplier = Supplier.Create(Ids.New(), tenantId, corePartyId, name, email, phone);
        await repository.AddSupplierAsync(supplier, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return supplier;
    }

    public async Task<Supplier> UpdateContactAsync(Guid supplierId, TenantId tenantId,
        string? email, string? phone, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetSupplierAsync(supplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.supplier_not_found");
        supplier.UpdateContact(email, phone);
        await repository.SaveChangesAsync(cancellationToken);
        return supplier;
    }

    public async Task<Supplier> SetActiveAsync(Guid supplierId, TenantId tenantId, bool active,
        CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetSupplierAsync(supplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("procurement.supplier_not_found");
        if (active) supplier.Activate(); else supplier.Deactivate();
        await repository.SaveChangesAsync(cancellationToken);
        return supplier;
    }
}
