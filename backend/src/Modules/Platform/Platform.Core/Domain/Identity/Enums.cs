namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Status of a platform user (Spec 01 §4.2, §5.2).
/// CHECK in ('invited', 'active', 'deactivated', 'locked').
/// </summary>
public enum UserStatus
{
    Invited,
    Active,
    Deactivated,
    Locked
}

/// <summary>
/// Origin client kind for issued refresh tokens (Spec 01 §4.2).
/// CHECK in ('web', 'mobile', 'api').
/// </summary>
public enum ClientKind
{
    Web,
    Mobile,
    Api
}

/// <summary>
/// Reason for refresh token revocation (Spec 01 §4.2, BR-PLT-05).
/// CHECK in ('logout', 'reuse_detected', 'admin', 'password_changed', 'deactivated').
/// </summary>
public enum RevokeReason
{
    Logout,
    ReuseDetected,
    Admin,
    PasswordChanged,
    Deactivated
}

public static class IdentityEnumExtensions
{
    public static string ToDbValue(this UserStatus status) => status switch
    {
        UserStatus.Invited => "invited",
        UserStatus.Active => "active",
        UserStatus.Deactivated => "deactivated",
        UserStatus.Locked => "locked",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static UserStatus ParseUserStatus(string value) => value.ToLowerInvariant() switch
    {
        "invited" => UserStatus.Invited,
        "active" => UserStatus.Active,
        "deactivated" => UserStatus.Deactivated,
        "locked" => UserStatus.Locked,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown UserStatus: {value}")
    };

    public static string ToDbValue(this ClientKind kind) => kind switch
    {
        ClientKind.Web => "web",
        ClientKind.Mobile => "mobile",
        ClientKind.Api => "api",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static ClientKind ParseClientKind(string value) => value.ToLowerInvariant() switch
    {
        "web" => ClientKind.Web,
        "mobile" => ClientKind.Mobile,
        "api" => ClientKind.Api,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown ClientKind: {value}")
    };

    public static string ToDbValue(this RevokeReason reason) => reason switch
    {
        RevokeReason.Logout => "logout",
        RevokeReason.ReuseDetected => "reuse_detected",
        RevokeReason.Admin => "admin",
        RevokeReason.PasswordChanged => "password_changed",
        RevokeReason.Deactivated => "deactivated",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    public static RevokeReason ParseRevokeReason(string value) => value.ToLowerInvariant() switch
    {
        "logout" => RevokeReason.Logout,
        "reuse_detected" => RevokeReason.ReuseDetected,
        "admin" => RevokeReason.Admin,
        "password_changed" => RevokeReason.PasswordChanged,
        "deactivated" => RevokeReason.Deactivated,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown RevokeReason: {value}")
    };
}
