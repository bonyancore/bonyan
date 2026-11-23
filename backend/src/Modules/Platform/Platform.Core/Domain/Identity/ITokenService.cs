using System.Security.Claims;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;

namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Service abstraction for signing and validating JWT access tokens (Spec 01 §7.2, BR-PLT-02).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed JWT access token carrying sub, tid, email, and jti claims.
    /// Expiration is 15 minutes.
    /// </summary>
    string GenerateAccessToken(
        UserId userId,
        TenantId tenantId,
        string email,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? permissions = null);

    /// <summary>
    /// Generates a cryptographically secure random opaque refresh token string (32 bytes).
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Computes the SHA-256 hash of a raw refresh token string for database storage and lookup.
    /// </summary>
    string HashRefreshToken(string rawToken);

    /// <summary>
    /// Validates an access token and extracts the ClaimsPrincipal if signature and lifetime are valid.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}
