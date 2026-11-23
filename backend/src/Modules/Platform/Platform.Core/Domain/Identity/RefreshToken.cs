using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Refresh token aggregate / record (Spec 01 §4.2: platform.refresh_tokens).
/// Single-use token rotated on every exchange (BR-PLT-05).
/// </summary>
public sealed class RefreshToken : ITenantScopedEntity
{
    private RefreshToken() { } // For EF Core

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public ClientKind ClientKind { get; private set; }
    public string? DeviceLabel { get; private set; }
    public string? Ip { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public RevokeReason? RevokeReason { get; private set; }

    // Created audit (note: refresh_tokens has no updated_* or soft delete per Spec 01 §4.2)
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public bool IsActive => RotatedAt == null && RevokedAt == null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public static RefreshToken Create(
        Guid id,
        TenantId tenantId,
        Guid userId,
        Guid familyId,
        string tokenHash,
        ClientKind clientKind,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? deviceLabel = null,
        string? ip = null,
        string? userAgent = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("RefreshTokenId cannot be empty.", nameof(id));

        tenantId.EnsureValid();

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (familyId == Guid.Empty)
            throw new ArgumentException("FamilyId cannot be empty.", nameof(familyId));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));

        if (expiresAt <= issuedAt)
            throw new ArgumentException("ExpiresAt must be after IssuedAt.", nameof(expiresAt));

        return new RefreshToken
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ClientKind = clientKind,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            DeviceLabel = deviceLabel?.Trim(),
            Ip = ip?.Trim(),
            UserAgent = userAgent?.Trim(),
            CreatedAt = issuedAt
        };
    }

    /// <summary>
    /// Rotates the refresh token (single-use semantics).
    /// Throws if the token was already rotated or revoked.
    /// </summary>
    public void Rotate(DateTimeOffset now)
    {
        if (RotatedAt.HasValue)
        {
            throw new InvalidOperationException("Refresh token has already been rotated.");
        }

        if (RevokedAt.HasValue)
        {
            throw new InvalidOperationException("Refresh token has already been revoked.");
        }

        RotatedAt = now;
    }

    /// <summary>
    /// Revokes the refresh token with a specified reason.
    /// </summary>
    public void Revoke(RevokeReason reason, DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
        {
            return; // Idempotent revocation
        }

        RevokedAt = now;
        RevokeReason = reason;
    }
}
