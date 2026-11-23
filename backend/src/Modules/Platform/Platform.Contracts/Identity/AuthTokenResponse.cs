namespace Construction.Platform.Contracts.Identity;

/// <summary>
/// Successful authentication token pair and expiration details (Spec 01 §7.2).
/// </summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer",
    UserProfileResponse? User = null);
