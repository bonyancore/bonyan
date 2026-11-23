namespace Construction.Platform.Contracts.Identity;

/// <summary>
/// User profile summary returned on login or /auth/me (Spec 01 §7.2).
/// </summary>
public sealed record UserProfileResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string? FullNameAr,
    string? FullNameEn,
    string? Phone,
    string? JobTitle,
    string Status,
    string Locale,
    string Numerals,
    string CalendarPref,
    string? TimeZone,
    bool MfaEnabled,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null);
