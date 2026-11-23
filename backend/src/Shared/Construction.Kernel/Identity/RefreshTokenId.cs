namespace Construction.Kernel.Identity;

public readonly record struct RefreshTokenId
{
    public Guid Value { get; init; }

    /// <summary>
    /// Indicates whether this RefreshTokenId represents a valid, non-empty refresh token identifier.
    /// Evaluates to false for default(RefreshTokenId) or Guid.Empty.
    /// </summary>
    public bool IsValid => Value != Guid.Empty;

    public RefreshTokenId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("RefreshTokenId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static RefreshTokenId New() => new(Ids.New());

    public static RefreshTokenId From(Guid value) => new(value);

    public void Deconstruct(out Guid value) => value = Value;

    /// <summary>Throws an InvalidOperationException if the struct is in an uninitialized/default state.</summary>
    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("RefreshTokenId is not initialized (default value is invalid).");
        }
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(RefreshTokenId id) => id.Value;
    public static explicit operator RefreshTokenId(Guid value) => new(value);
}
