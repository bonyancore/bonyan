using Construction.Kernel;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;

namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Primary identity and authentication service interface (Spec 01 §3 AuthService, §7.2).
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Authenticates a user using email and password, enforcing lockout rules and tenant suspension checks.
    /// Note: LoginRequest.MfaCode is accepted by the contract for forward compatibility, but MFA verification
    /// is not yet implemented in Phase 1 / Phase 2 and is currently ignored.
    /// </summary>
    Task<Result<AuthTokenResponse>> LoginAsync(
        LoginRequest request,
        string? ip = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically rotates an opaque refresh token with concurrency locking and reuse family revocation.
    /// </summary>
    Task<Result<AuthTokenResponse>> RefreshTokenAsync(
        string rawRefreshToken,
        string? ip = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an active session / refresh token family.
    /// </summary>
    Task<Result> LogoutAsync(
        UserId userId,
        Guid? familyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the profile, roles, and effective permissions for the authenticated caller.
    /// </summary>
    Task<Result<UserProfileResponse>> GetCurrentUserProfileAsync(
        UserId userId,
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
