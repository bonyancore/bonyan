namespace Construction.Kernel.Users;

/// <summary>
/// Baseline fallback implementation of IUserContext representing an unauthenticated caller.
/// </summary>
public sealed class NullUserContext : IUserContext
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
}

