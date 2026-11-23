using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Construction.Platform.Infrastructure.Identity;

/// <summary>
/// Service abstraction for signing and validating JWT access tokens and managing refresh token hashes.
/// Spec 01 §7.2, Spec 00 §3.8, BR-PLT-02.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly IdentityOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(IOptions<IdentityOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.JwtSigningKey) || Encoding.UTF8.GetByteCount(_options.JwtSigningKey) < 32)
        {
            throw new InvalidOperationException("IdentityOptions.JwtSigningKey must be at least 256 bits (32 bytes).");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSigningKey));
        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
    }

    public string GenerateAccessToken(
        UserId userId,
        TenantId tenantId,
        string email,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? permissions = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tid", tenantId.Value.ToString()),
            new("tenant_id", tenantId.Value.ToString())
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("roles", role));
            }
        }

        if (permissions != null)
        {
            foreach (var perm in permissions)
            {
                claims.Add(new Claim("permissions", perm));
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return _tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Base64UrlEncoder.Encode(randomBytes);
    }

    public string HashRefreshToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Raw refresh token cannot be empty.", nameof(rawToken));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuer = !string.IsNullOrWhiteSpace(_options.Issuer),
            ValidIssuer = _options.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_options.Audience),
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(token, parameters, out var securityToken);
            if (securityToken is JwtSecurityToken jwtToken &&
                jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return principal;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
