using Construction.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Procurement.Infrastructure.Persistence.Configurations;

internal static class ProcurementConfigurationHelpers
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder) where T : class, IAuditableEntity
    {
        builder.Property(entity => entity.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasColumnType("uuid");
        builder.Property(entity => entity.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.UpdatedBy).HasColumnType("uuid");
    }

    public static void ConfigureStatus<TEnum>(this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum => property.HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<TEnum>(value, true)).HasMaxLength(24).IsRequired();
}
