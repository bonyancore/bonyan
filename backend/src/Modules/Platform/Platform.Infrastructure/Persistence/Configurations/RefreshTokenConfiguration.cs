using Construction.Platform.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for RefreshToken (platform.refresh_tokens) per Spec 01 §4.2.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", PlatformDbContext.Schema, t =>
        {
            t.HasCheckConstraint("ck_refresh_tokens_client_kind", "client_kind IN ('web', 'mobile', 'api')");
            t.HasCheckConstraint("ck_refresh_tokens_revoke_reason", "revoke_reason IS NULL OR revoke_reason IN ('logout', 'reuse_detected', 'admin', 'password_changed', 'deactivated')");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.FamilyId).IsRequired();

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(r => r.TokenHash)
            .IsUnique();

        builder.Property(r => r.ClientKind)
            .IsRequired()
            .HasConversion(
                k => k.ToDbValue(),
                v => IdentityEnumExtensions.ParseClientKind(v))
            .HasMaxLength(32);

        builder.Property(r => r.DeviceLabel).HasMaxLength(200);

        builder.Property(r => r.Ip)
            .HasMaxLength(45);

        builder.Property(r => r.UserAgent).HasMaxLength(500);
        builder.Property(r => r.IssuedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        builder.Property(r => r.RevokeReason)
            .HasConversion(
                r => r.HasValue ? r.Value.ToDbValue() : null,
                v => !string.IsNullOrEmpty(v) ? IdentityEnumExtensions.ParseRevokeReason(v) : null)
            .HasMaxLength(32);

        // Spec 01 §4.2 Indexes
        builder.HasIndex(r => new { r.UserId, r.RevokedAt });
        builder.HasIndex(r => r.FamilyId);
        builder.HasIndex(r => new { r.TenantId, r.FamilyId });

        // Relational composite foreign key to User ensuring refresh_tokens.tenant_id == users.tenant_id
        builder.HasOne<User>()
            .WithMany()
            .HasPrincipalKey(u => new { u.TenantId, u.Id })
            .HasForeignKey(r => new { r.TenantId, r.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
