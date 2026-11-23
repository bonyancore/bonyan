namespace Construction.Platform.Core.Domain.Identity;

/// <summary>
/// Password hashing abstraction using Argon2id (Spec 01 §6.1 BR-PLT-06).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes the plain-text password using Argon2id with unique salt.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies the plain-text password against a Modular Crypt Format hash.
    /// Returns true on match; false on mismatch. Constant-time comparison.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// Computes a dummy hash verification in constant time to prevent timing attacks
    /// when an email does not exist in the database.
    /// </summary>
    bool VerifyDummy();
}
