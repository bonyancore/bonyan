using Construction.Platform.Core.Domain.Identity;
using Xunit;

namespace Construction.Platform.Domain.Tests.Identity;

public class IdentityEnumTests
{
    [Theory]
    [InlineData(UserStatus.Invited, "invited")]
    [InlineData(UserStatus.Active, "active")]
    [InlineData(UserStatus.Deactivated, "deactivated")]
    [InlineData(UserStatus.Locked, "locked")]
    public void UserStatus_ToDbValue_And_Parse_Roundtrip(UserStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, IdentityEnumExtensions.ParseUserStatus(dbValue));
    }

    [Theory]
    [InlineData(ClientKind.Web, "web")]
    [InlineData(ClientKind.Mobile, "mobile")]
    [InlineData(ClientKind.Api, "api")]
    public void ClientKind_ToDbValue_And_Parse_Roundtrip(ClientKind kind, string dbValue)
    {
        Assert.Equal(dbValue, kind.ToDbValue());
        Assert.Equal(kind, IdentityEnumExtensions.ParseClientKind(dbValue));
    }

    [Theory]
    [InlineData(RevokeReason.Logout, "logout")]
    [InlineData(RevokeReason.ReuseDetected, "reuse_detected")]
    [InlineData(RevokeReason.Admin, "admin")]
    [InlineData(RevokeReason.PasswordChanged, "password_changed")]
    [InlineData(RevokeReason.Deactivated, "deactivated")]
    public void RevokeReason_ToDbValue_And_Parse_Roundtrip(RevokeReason reason, string dbValue)
    {
        Assert.Equal(dbValue, reason.ToDbValue());
        Assert.Equal(reason, IdentityEnumExtensions.ParseRevokeReason(dbValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("invalid_status")]
    public void ParseUserStatus_WithInvalidValue_ThrowsArgumentOutOfRangeException(string invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdentityEnumExtensions.ParseUserStatus(invalid));

        Assert.Equal("value", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("desktop")]
    public void ParseClientKind_WithInvalidValue_ThrowsArgumentOutOfRangeException(string invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdentityEnumExtensions.ParseClientKind(invalid));

        Assert.Equal("value", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("token_expired")]
    public void ParseRevokeReason_WithInvalidValue_ThrowsArgumentOutOfRangeException(string invalid)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdentityEnumExtensions.ParseRevokeReason(invalid));

        Assert.Equal("value", ex.ParamName);
    }
}
