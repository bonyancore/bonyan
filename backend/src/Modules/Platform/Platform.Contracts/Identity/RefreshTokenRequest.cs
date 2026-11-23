namespace Construction.Platform.Contracts.Identity;

/// <summary>
/// Request payload for rotating a refresh token (Spec 01 §7.2: POST /auth/refresh).
/// </summary>
public sealed record RefreshTokenRequest(
    string RefreshToken);
