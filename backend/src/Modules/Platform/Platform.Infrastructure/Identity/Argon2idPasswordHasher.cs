using System.Security.Cryptography;
using System.Text;
using Construction.Platform.Core.Domain.Identity;
using Isopoh.Cryptography.Argon2;
using Microsoft.Extensions.Options;

namespace Construction.Platform.Infrastructure.Identity;

/// <summary>
/// Password hashing and verification using Argon2id per Spec 01 §4.2.
/// Formats and parses Modular Crypt Format (MCF) strings: $argon2id$v=19$m=...,t=...,p=...$...$...
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private readonly IdentityOptions _options;

    public Argon2idPasswordHasher(IOptions<IdentityOptions>? options = null)
    {
        _options = options?.Value ?? new IdentityOptions();
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing, // Argon2id
            Version = Argon2Version.Nineteen,
            MemoryCost = _options.Argon2MemoryCostKiB,
            TimeCost = _options.Argon2TimeCost,
            Lanes = _options.Argon2Parallelism,
            Threads = _options.Argon2Parallelism,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = salt,
            HashLength = 32
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return Argon2.Verify(passwordHash, Encoding.UTF8.GetBytes(password));
        }
        catch
        {
            return false;
        }
    }

    private static readonly string DummyHash = "$argon2id$v=19$m=65536,t=3,p=1$c29tZXNhbHRzb21lc2FsdA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public bool VerifyDummy()
    {
        try
        {
            Argon2.Verify(DummyHash, Encoding.UTF8.GetBytes("DummyPasswordToPreventTimingAttacks!"));
        }
        catch
        {
            // Suppress exception; always return false
        }

        return false;
    }
}
