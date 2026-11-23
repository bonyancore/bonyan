namespace Construction.Platform.Contracts.Identity;

/// <summary>
/// Request payload for password login (Spec 01 §7.2: POST /auth/login).
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Password">User plaintext password.</param>
/// <param name="MfaCode">
/// Optional TOTP MFA code. Accepted by the contract for forward compatibility per Spec 01 §7.2,
/// but MFA verification is not yet implemented in Phase 1 / Phase 2 and is currently ignored.
/// </param>
public sealed record LoginRequest(
    string Email,
    string Password,
    string? MfaCode = null);
