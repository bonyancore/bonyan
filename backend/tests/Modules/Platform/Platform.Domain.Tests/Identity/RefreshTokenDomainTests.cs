using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Identity;
using Xunit;

namespace Construction.Platform.Domain.Tests.Identity;

public class RefreshTokenDomainTests
{
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_WithValidParameters_ReturnsActiveToken()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddDays(30);

        var token = RefreshToken.Create(
            id, _tenantId, userId, familyId, "hash123",
            ClientKind.Web, issuedAt, expiresAt, "Chrome", "192.168.1.1", "Mozilla/5.0");

        Assert.Equal(id, token.Id);
        Assert.Equal(_tenantId, token.TenantId);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Equal("hash123", token.TokenHash);
        Assert.Equal(ClientKind.Web, token.ClientKind);
        Assert.Equal("Chrome", token.DeviceLabel);
        Assert.Equal("192.168.1.1", token.Ip);
        Assert.Equal("Mozilla/5.0", token.UserAgent);
        Assert.Equal(issuedAt, token.IssuedAt);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Null(token.RotatedAt);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.RevokeReason);
        Assert.True(token.IsActive);
    }

    [Fact]
    public void Create_WithInvalidExpiry_ThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(
                Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
                "hash", ClientKind.Web, now, now.AddMinutes(-5)));
    }

    [Fact]
    public void Create_WhereExpiresAtEqualsIssuedAt_ThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(
                Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
                "hash", ClientKind.Web, now, now));

        Assert.Equal("expiresAt", ex.ParamName);
        Assert.Contains("ExpiresAt must be after IssuedAt", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyOrWhitespaceTokenHash_ThrowsArgumentException(string tokenHash)
    {
        var now = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(
                Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
                tokenHash, ClientKind.Web, now, now.AddDays(30)));

        Assert.Equal("tokenHash", ex.ParamName);
        Assert.Contains("Token hash cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithNullTokenHash_ThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(
                Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
                null!, ClientKind.Web, now, now.AddDays(30)));

        Assert.Equal("tokenHash", ex.ParamName);
    }

    [Fact]
    public void Rotate_WhenActive_SetsRotatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        token.Rotate(now.AddMinutes(5));

        Assert.Equal(now.AddMinutes(5), token.RotatedAt);
        Assert.False(token.IsActive);
    }

    [Fact]
    public void Rotate_WhenAlreadyRotated_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        token.Rotate(now.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() => token.Rotate(now.AddMinutes(10)));
    }

    [Fact]
    public void Rotate_WhenRevoked_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        token.Revoke(RevokeReason.Logout, now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => token.Rotate(now.AddMinutes(5)));
    }

    [Fact]
    public void Revoke_SetsRevokedAtAndReason()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        token.Revoke(RevokeReason.ReuseDetected, now.AddMinutes(5));

        Assert.Equal(now.AddMinutes(5), token.RevokedAt);
        Assert.Equal(RevokeReason.ReuseDetected, token.RevokeReason);
        Assert.False(token.IsActive);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        token.Revoke(RevokeReason.Logout, now.AddMinutes(2));
        token.Revoke(RevokeReason.Admin, now.AddMinutes(10));

        Assert.Equal(now.AddMinutes(2), token.RevokedAt);
        Assert.Equal(RevokeReason.Logout, token.RevokeReason);
    }

    [Fact]
    public void IsExpired_CorrectlyEvaluatesAgainstTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), Guid.NewGuid(),
            "hash", ClientKind.Web, now, now.AddDays(30));

        Assert.False(token.IsExpired(now.AddDays(29)));
        Assert.True(token.IsExpired(now.AddDays(30)));
        Assert.True(token.IsExpired(now.AddDays(31)));
    }
}
