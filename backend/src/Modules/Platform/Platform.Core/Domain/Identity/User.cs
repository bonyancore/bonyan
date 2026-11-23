using Construction.Kernel;
using Construction.Kernel.Identity;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;

namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Platform user entity (Spec 01 §4.2: platform.users).
/// Enforces business rules BR-PLT-03 (lockout), BR-PLT-04 (admin preservation), etc.
/// </summary>
public sealed class User : ITenantScopedEntity, IAuditableEntity
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { } // For EF Core

    public Guid Id { get; private set; }
    public TenantId TenantId { get; set; }
    public string Email { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? FullNameAr { get; private set; }
    public string? FullNameEn { get; private set; }
    public string? JobTitle { get; private set; }
    public UserStatus Status { get; private set; } = UserStatus.Invited;
    public string? PasswordHash { get; private set; }
    public DateTimeOffset? PasswordChangedAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? FailedLoginWindowStartedAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool MfaEnabled { get; private set; }
    public byte[]? MfaSecretEnc { get; private set; }
    public string? MfaRecoveryHashes { get; private set; }
    public string Locale { get; private set; } = "ar";
    public string Numerals { get; private set; } = "latin";
    public string CalendarPref { get; private set; } = "gregorian";
    public string? TimeZone { get; private set; }
    public Guid? AvatarFileId { get; private set; }
    public Guid? SignatureFileId { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }
    public Guid? DeactivatedBy { get; private set; }
    public string? DeactivationReason { get; private set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static User Create(
        Guid id,
        TenantId tenantId,
        string email,
        string? fullNameAr,
        string? fullNameEn,
        string? phone = null,
        string? jobTitle = null,
        string locale = "ar",
        string numerals = "latin",
        string calendarPref = "gregorian",
        string? timeZone = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(id));

        tenantId.EnsureValid();

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(fullNameAr) && string.IsNullOrWhiteSpace(fullNameEn))
            throw new DomainRuleViolation("platform.user_name_required", "At least one name (Arabic or English) is required.");

        return new User
        {
            Id = id,
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            FullNameAr = fullNameAr?.Trim(),
            FullNameEn = fullNameEn?.Trim(),
            Phone = phone?.Trim(),
            JobTitle = jobTitle?.Trim(),
            Status = UserStatus.Invited,
            Locale = locale,
            Numerals = numerals,
            CalendarPref = calendarPref,
            TimeZone = timeZone,
            FailedLoginCount = 0
        };
    }

    public void SetPassword(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

        PasswordHash = passwordHash;
        PasswordChangedAt = now;

        if (Status == UserStatus.Invited)
        {
            Status = UserStatus.Active;
        }
    }

    /// <summary>
    /// Pure predicate: evaluates whether the account is currently locked out at the given timestamp.
    /// Does not mutate entity state.
    /// Returns true if the account is in Locked status or LockedUntil is in the future relative to <paramref name="now"/>.
    /// </summary>
    public bool IsLockedOut(DateTimeOffset now)
    {
        if (LockedUntil.HasValue && now < LockedUntil.Value)
        {
            return true;
        }

        return Status == UserStatus.Locked && (!LockedUntil.HasValue || now < LockedUntil.Value);
    }

    /// <summary>
    /// Explicit operation to clear lockout state if the lockout duration has elapsed (Spec 01 §6.1 BR-PLT-03).
    /// If LockedUntil is in the past or present and status is Locked, clears lockout fields and restores status to Active.
    /// Returns true if an expired lockout was cleared; false if still locked or not locked.
    /// </summary>
    public bool ClearExpiredLockout(DateTimeOffset now)
    {
        if (Status == UserStatus.Locked && LockedUntil.HasValue && now >= LockedUntil.Value)
        {
            LockedUntil = null;
            FailedLoginCount = 0;
            FailedLoginWindowStartedAt = null;
            Status = UserStatus.Active;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records a failed login attempt (Spec 01 §6.1 BR-PLT-03).
    /// 5 failures within 15 minutes lock the account for 15 minutes.
    /// Only Active accounts can accrue failures and transition to Locked (Spec 01 §5.2).
    /// Attempts on non-Active accounts (Invited, Deactivated, or already Locked) do not mutate lockout state.
    /// Returns true if this attempt triggered a lockout.
    /// </summary>
    public bool RecordFailedLogin(DateTimeOffset now)
    {
        // Only Active accounts accrue failed logins and can transition to Locked (Spec 01 §5.2)
        if (Status != UserStatus.Active)
        {
            return false;
        }

        // Check if existing window expired
        if (FailedLoginWindowStartedAt == null || (now - FailedLoginWindowStartedAt.Value) > FailureWindow)
        {
            FailedLoginCount = 1;
            FailedLoginWindowStartedAt = now;
        }
        else
        {
            FailedLoginCount++;
        }

        if (FailedLoginCount >= MaxFailedAttempts)
        {
            Status = UserStatus.Locked;
            LockedUntil = now + LockoutDuration;
            FailedLoginCount = 0;
            FailedLoginWindowStartedAt = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the failed login counter upon successful authentication (Spec 01 §6.1 BR-PLT-03).
    /// </summary>
    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        FailedLoginWindowStartedAt = null;
        LockedUntil = null;
        LastLoginAt = now;
        if (Status == UserStatus.Locked)
        {
            Status = UserStatus.Active;
        }
    }

    public void Deactivate(Guid actorId, string? reason, DateTimeOffset now)
    {
        Status = UserStatus.Deactivated;
        DeactivatedAt = now;
        DeactivatedBy = actorId;
        DeactivationReason = reason?.Trim();
        LockedUntil = null;
        FailedLoginCount = 0;
        FailedLoginWindowStartedAt = null;
    }

    public void Reactivate()
    {
        Status = UserStatus.Active;
        DeactivatedAt = null;
        DeactivatedBy = null;
        DeactivationReason = null;
    }
}
