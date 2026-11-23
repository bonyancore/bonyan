using System.Text;
using Microsoft.Extensions.Hosting;

namespace Construction.Platform.Infrastructure.Identity;

/// <summary>
/// Configuration options for Identity & Authentication services (Spec 01 §3 AuthService, §4.2, Spec 00 §3.8).
/// </summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Fallback signing key strictly reserved for local development and non-production testing.
    /// Never allowed in Production, Staging, or other deployed environments.
    /// </summary>
    public const string DevelopmentDefaultSigningKey = "BonyanSuperSecretDefaultSigningKeyForDevelopmentPurposesMustBeAtLeast256BitsLong!";

    /// <summary>
    /// Cryptographic signing key for JWT access tokens. Must be at least 256 bits (32 bytes).
    /// Defaults to empty to prevent accidental startup with insecure secrets in production environments.
    /// </summary>
    public string JwtSigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Expected JWT token issuer.
    /// </summary>
    public string Issuer { get; set; } = "https://identity.bonyan.example";

    /// <summary>
    /// Expected JWT token audience.
    /// </summary>
    public string Audience { get; set; } = "https://api.bonyan.example";

    /// <summary>
    /// Access token lifetime in minutes. Standard is 15 minutes per Spec 00 §3.8.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token lifetime in days. Standard is 30 days per Spec 01 §4.2.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    /// Argon2id memory cost in KiB. Production default: 65536 KiB (64 MB).
    /// </summary>
    public int Argon2MemoryCostKiB { get; set; } = 65536;

    /// <summary>
    /// Argon2id time cost (iterations). Production default: 3.
    /// </summary>
    public int Argon2TimeCost { get; set; } = 3;

    /// <summary>
    /// Argon2id lanes / degree of parallelism. Production default: 1.
    /// </summary>
    public int Argon2Parallelism { get; set; } = 1;

    /// <summary>
    /// Validates the options instance against the current hosting environment.
    /// Enforces key presence, minimum length, and strictly forbids the development fallback key outside Development.
    /// Diagnostic errors intentionally do not expose key contents.
    /// </summary>
    public void ValidateForEnvironment(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(JwtSigningKey))
            {
                JwtSigningKey = DevelopmentDefaultSigningKey;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(JwtSigningKey))
        {
            throw new InvalidOperationException(
                "Identity:JwtSigningKey must be configured in non-Development environments. Missing secret key.");
        }

        if (string.Equals(JwtSigningKey, DevelopmentDefaultSigningKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Identity:JwtSigningKey cannot use the hardcoded development fallback key in non-Development environments.");
        }

        if (Encoding.UTF8.GetByteCount(JwtSigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Identity:JwtSigningKey must be at least 256 bits (32 bytes).");
        }
    }
}
