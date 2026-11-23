using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for User (platform.users) per Spec 01 §4.2.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", PlatformDbContext.Schema, t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('invited', 'active', 'deactivated', 'locked')");
            t.HasCheckConstraint("ck_users_active_password", "status <> 'active' OR password_hash IS NOT NULL");
            t.HasCheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .IsRequired();

        // citext email unique across all tenants in v1 per Spec 01 §4.2
        builder.Property(u => u.Email)
            .IsRequired()
            .HasColumnType("citext")
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.Phone)
            .HasMaxLength(32);

        builder.HasIndex(u => new { u.TenantId, u.Phone })
            .IsUnique()
            .HasFilter("phone IS NOT NULL");

        builder.Property(u => u.FullNameAr).HasMaxLength(200);
        builder.Property(u => u.FullNameEn).HasMaxLength(200);
        builder.Property(u => u.JobTitle).HasMaxLength(100);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion(
                s => s.ToDbValue(),
                v => IdentityEnumExtensions.ParseUserStatus(v))
            .HasMaxLength(32);

        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.FailedLoginCount).IsRequired().HasDefaultValue(0);

        builder.Property(u => u.Locale).IsRequired().HasMaxLength(10).HasDefaultValue("ar");
        builder.Property(u => u.Numerals).IsRequired().HasMaxLength(20).HasDefaultValue("latin");
        builder.Property(u => u.CalendarPref).IsRequired().HasMaxLength(20).HasDefaultValue("gregorian");
        builder.Property(u => u.TimeZone).HasMaxLength(50);
        builder.Property(u => u.DeactivationReason).HasMaxLength(500);

        // Composite unique on (tenant_id, id) to support composite FK from refresh_tokens
        builder.HasIndex(u => new { u.TenantId, u.Id })
            .IsUnique();

        // Spec 01 §4.2 Indexes
        builder.HasIndex(u => new { u.TenantId, u.Status });
        builder.HasIndex(u => new { u.TenantId, u.ManagerUserId });
    }
}
