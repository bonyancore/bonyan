using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Construction.Persistence.ValueConverters;

/// <summary>
/// Converts strongly-typed TenantId value object to and from PostgreSQL UUID.
/// Enforces validity on conversion from raw database value.
/// </summary>
public sealed class TenantIdValueConverter() : ValueConverter<TenantId, Guid>(
    id => id.Value,
    guid => TenantId.From(guid));

