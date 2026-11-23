using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Identity;
using Xunit;

namespace Construction.Platform.Domain.Tests.Identity;

public class UserDomainTests
{
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_WithValidData_ReturnsInvitedUser()
    {
        var id = Guid.NewGuid();
        var user = User.Create(id, _tenantId, "test@example.com", "أحمد", "Ahmed");

        Assert.Equal(id, user.Id);
        Assert.Equal(_tenantId, user.TenantId);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("أحمد", user.FullNameAr);
        Assert.Equal("Ahmed", user.FullNameEn);
        Assert.Equal(UserStatus.Invited, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            User.Create(Guid.Empty, _tenantId, "test@example.com", "أحمد", "Ahmed"));

        Assert.Equal("id", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyOrWhitespaceEmail_ThrowsArgumentException(string email)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            User.Create(Guid.NewGuid(), _tenantId, email, "أحمد", "Ahmed"));

        Assert.Equal("email", ex.ParamName);
    }

    [Fact]
    public void Create_WithNullEmail_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            User.Create(Guid.NewGuid(), _tenantId, null!, "أحمد", "Ahmed"));

        Assert.Equal("email", ex.ParamName);
    }

    [Fact]
    public void Create_WithoutNames_ThrowsDomainRuleViolation()
    {
        var id = Guid.NewGuid();
        var ex = Assert.Throws<DomainRuleViolation>(() =>
            User.Create(id, _tenantId, "test@example.com", null, null));

        Assert.Equal("platform.user_name_required", ex.Code);
    }

    [Fact]
    public void SetPassword_SetsHashAndTransitionsStatusToActive()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;

        user.SetPassword("$argon2id$v=19$...", now);

        Assert.Equal("$argon2id$v=19$...", user.PasswordHash);
        Assert.Equal(now, user.PasswordChangedAt);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_FirstFourAttempts_IncrementsCountWithoutLocking()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 4; i++)
        {
            var locked = user.RecordFailedLogin(now.AddMinutes(i));
            Assert.False(locked);
            Assert.Equal(i, user.FailedLoginCount);
            Assert.Null(user.LockedUntil);
            Assert.Equal(UserStatus.Active, user.Status);
        }
    }

    [Fact]
    public void RecordFailedLogin_FifthAttemptWithin15Min_LocksUserAndSets15MinLockout()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 4; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        var locked = user.RecordFailedLogin(now.AddMinutes(5));

        Assert.True(locked);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);
        Assert.Equal(now.AddMinutes(5).AddMinutes(15), user.LockedUntil.Value);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void RecordFailedLogin_ActiveUser_FifthAttemptWithin15Min_LocksUserAndSets15MinLockout()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);
        Assert.Equal(UserStatus.Active, user.Status);

        for (int i = 1; i <= 4; i++)
        {
            var locked = user.RecordFailedLogin(now.AddMinutes(i));
            Assert.False(locked);
            Assert.Equal(i, user.FailedLoginCount);
            Assert.Null(user.LockedUntil);
            Assert.Equal(UserStatus.Active, user.Status);
        }

        var fifthLocked = user.RecordFailedLogin(now.AddMinutes(5));

        Assert.True(fifthLocked);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);
        Assert.Equal(now.AddMinutes(5).AddMinutes(15), user.LockedUntil.Value);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void RecordFailedLogin_After15MinWindow_ResetsWindowCountToOne()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        user.RecordFailedLogin(now);
        user.RecordFailedLogin(now.AddMinutes(2));
        Assert.Equal(2, user.FailedLoginCount);

        // Next failure is 16 minutes after window started
        var locked = user.RecordFailedLogin(now.AddMinutes(16));

        Assert.False(locked);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Equal(now.AddMinutes(16), user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void IsLockedOut_PurePredicate_DoesNotMutateState()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        var lockedUntil = user.LockedUntil!.Value;

        // 1. Before expiration: locked
        Assert.True(user.IsLockedOut(lockedUntil.AddMinutes(-5)));
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);

        // 2. Exact boundary at LockedUntil: no longer strictly before LockedUntil
        Assert.False(user.IsLockedOut(lockedUntil));
        // Status remains Locked and LockedUntil remains set because IsLockedOut is pure
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);

        // 3. Immediately after boundary: still pure, does not mutate
        Assert.False(user.IsLockedOut(lockedUntil.AddTicks(1)));
        Assert.False(user.IsLockedOut(lockedUntil.AddMinutes(5)));
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);
    }

    [Fact]
    public void ClearExpiredLockout_ExplicitlyClearsLockoutStateAndRestoresActiveStatus()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        var lockedUntil = user.LockedUntil!.Value;

        // Before expiration: cannot clear
        var clearedBefore = user.ClearExpiredLockout(lockedUntil.AddMinutes(-1));
        Assert.False(clearedBefore);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);

        // Exactly at LockedUntil: expired lockout is cleared
        var clearedAtBoundary = user.ClearExpiredLockout(lockedUntil);
        Assert.True(clearedAtBoundary);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.FailedLoginWindowStartedAt);
        Assert.Equal(UserStatus.Active, user.Status);

        // Calling again when not locked returns false
        Assert.False(user.ClearExpiredLockout(lockedUntil.AddMinutes(1)));
    }

    [Fact]
    public void ClearExpiredLockout_ImmediatelyAfterBoundary_ClearsLockoutState()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        var lockedUntil = user.LockedUntil!.Value;

        var cleared = user.ClearExpiredLockout(lockedUntil.AddTicks(1));
        Assert.True(cleared);
        Assert.Null(user.LockedUntil);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void RecordSuccessfulLogin_ClearsCountersAndUpdatesLastLogin()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        user.RecordFailedLogin(now);
        Assert.Equal(1, user.FailedLoginCount);

        user.RecordSuccessfulLogin(now.AddMinutes(1));

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.FailedLoginWindowStartedAt);
        Assert.Null(user.LockedUntil);
        Assert.Equal(now.AddMinutes(1), user.LastLoginAt);
    }

    [Fact]
    public void Deactivate_SetsDeactivatedStatusAndMetadata()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        user.Deactivate(actorId, "Departed company", now);

        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.Equal(actorId, user.DeactivatedBy);
        Assert.Equal("Departed company", user.DeactivationReason);
        Assert.Equal(now, user.DeactivatedAt);
    }

    [Fact]
    public void Reactivate_RestoresActiveStatus()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        user.Deactivate(Guid.NewGuid(), "Leave", DateTimeOffset.UtcNow);

        user.Reactivate();

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.DeactivatedAt);
        Assert.Null(user.DeactivatedBy);
        Assert.Null(user.DeactivationReason);
    }

    [Fact]
    public void RecordFailedLogin_WhenUserIsInvited_DoesNotLockOrChangeStatus()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(UserStatus.Invited, user.Status);

        for (int i = 1; i <= 5; i++)
        {
            var locked = user.RecordFailedLogin(now.AddMinutes(i));
            Assert.False(locked);
            Assert.Equal(0, user.FailedLoginCount);
            Assert.Null(user.LockedUntil);
            Assert.Equal(UserStatus.Invited, user.Status);
        }

        // Even after 25 minutes, expired lockout clear does not promote invited user
        var cleared = user.ClearExpiredLockout(now.AddMinutes(25));
        Assert.False(cleared);
        Assert.Equal(UserStatus.Invited, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_WhenUserIsDeactivated_DoesNotLockOrChangeStatus()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);
        user.Deactivate(Guid.NewGuid(), "Admin action", now.AddMinutes(1));
        Assert.Equal(UserStatus.Deactivated, user.Status);

        for (int i = 1; i <= 5; i++)
        {
            var locked = user.RecordFailedLogin(now.AddMinutes(5 + i));
            Assert.False(locked);
            Assert.Equal(0, user.FailedLoginCount);
            Assert.Null(user.LockedUntil);
            Assert.Equal(UserStatus.Deactivated, user.Status);
        }

        var cleared = user.ClearExpiredLockout(now.AddMinutes(35));
        Assert.False(cleared);
        Assert.Equal(UserStatus.Deactivated, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_WhenUserIsAlreadyLocked_DoesNotExtendOrMutateLockout()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        Assert.Equal(UserStatus.Locked, user.Status);
        var initialLockedUntil = user.LockedUntil;
        Assert.NotNull(initialLockedUntil);
        Assert.Equal(now.AddMinutes(5).AddMinutes(15), initialLockedUntil.Value);

        // Subsequent failure while locked: must return false, must not extend LockedUntil, must not increment count
        var triggered = user.RecordFailedLogin(now.AddMinutes(10));
        Assert.False(triggered);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(initialLockedUntil, user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Deactivate_WhenUserIsLocked_TransitionsToDeactivatedAndClearsLockoutFields()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.LockedUntil);

        var actorId = Guid.NewGuid();
        user.Deactivate(actorId, "Security incident", now.AddMinutes(6));

        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.Equal(actorId, user.DeactivatedBy);
        Assert.Equal("Security incident", user.DeactivationReason);
        Assert.Equal(now.AddMinutes(6), user.DeactivatedAt);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void ClearExpiredLockout_WhenUserIsDeactivated_ReturnsFalseAndPreservesDeactivation()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.Deactivate(Guid.NewGuid(), "Admin action", now);

        var cleared = user.ClearExpiredLockout(now.AddMinutes(30));
        Assert.False(cleared);
        Assert.Equal(UserStatus.Deactivated, user.Status);
    }

    [Fact]
    public void Reactivate_AfterDeactivationOfLockedUser_RestoresCleanActiveStatus()
    {
        var user = User.Create(Guid.NewGuid(), _tenantId, "test@example.com", "أحمد", null);
        var now = DateTimeOffset.UtcNow;
        user.SetPassword("$argon2id$v=19$...", now);

        for (int i = 1; i <= 5; i++)
        {
            user.RecordFailedLogin(now.AddMinutes(i));
        }

        Assert.Equal(UserStatus.Locked, user.Status);

        user.Deactivate(Guid.NewGuid(), "Temp lockup", now.AddMinutes(6));
        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.Null(user.LockedUntil);

        user.Reactivate();
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.False(user.IsLockedOut(now.AddMinutes(7)));
    }
}
