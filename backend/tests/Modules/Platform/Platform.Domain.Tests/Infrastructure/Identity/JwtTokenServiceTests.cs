using System.Security.Claims;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Construction.Platform.Domain.Tests.Infrastructure.Identity;

public sealed class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        var options = Options.Create(new IdentityOptions
        {
            JwtSigningKey = "ASecretKeyThatIsAtLeast32BytesLongForTestingPurposes!",
            Issuer = "https://identity.test.bonyan.example",
            Audience = "https://api.test.bonyan.example",
            AccessTokenExpirationMinutes = 15
        });
        _service = new JwtTokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_GeneratesValidSignedTokenWithClaims()
    {
        var userId = UserId.New();
        var tenantId = TenantId.New();
        var email = "engineer@example.com";

        var token = _service.GenerateAccessToken(userId, tenantId, email);

        Assert.NotNull(token);
        var principal = _service.ValidateToken(token);
        Assert.NotNull(principal);

        var sub = principal.FindFirst("sub")?.Value;
        var tid = principal.FindFirst("tid")?.Value;
        var tokenEmail = principal.FindFirst("email")?.Value;

        Assert.Equal(userId.Value.ToString(), sub);
        Assert.Equal(tenantId.Value.ToString(), tid);
        Assert.Equal(email, tokenEmail);
    }

    [Fact]
    public void GenerateRefreshToken_Returns32ByteUrlSafeToken()
    {
        var token1 = _service.GenerateRefreshToken();
        var token2 = _service.GenerateRefreshToken();

        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void HashRefreshToken_ReturnsConsistentSha256Hex()
    {
        var raw = "sample-token-string";
        var hash1 = _service.HashRefreshToken(raw);
        var hash2 = _service.HashRefreshToken(raw);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
