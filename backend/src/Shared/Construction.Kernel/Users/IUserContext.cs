namespace Construction.Kernel.Users;

/// <summary>
/// Provides identity of the current caller. Resolved from token claims by PLT-106.
/// PLT-103 registers a null-object implementation; PLT-106 replaces it.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// True if a valid, authenticated principal is present.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authenticated user's identity.
    /// INVARIANT: Returns null if and only if IsAuthenticated is false.
    /// INVARIANT: Returns a non-null Guid if and only if IsAuthenticated is true.
    /// Any implementation violating this invariant is invalid.
    /// </summary>
    Guid? UserId { get; }
}

