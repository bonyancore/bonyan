namespace Construction.Kernel.Identity;

public readonly record struct UserId
{
    public Guid Value { get; init; }

    /// <summary>
    /// Indicates whether this UserId represents a valid, non-empty user identifier.
    /// Evaluates to false for default(UserId) or Guid.Empty.
    /// </summary>
    public bool IsValid => Value != Guid.Empty;

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static UserId New() => new(Ids.New());

    public static UserId From(Guid value) => new(value);

    public void Deconstruct(out Guid value) => value = Value;

    /// <summary>Throws an InvalidOperationException if the struct is in an uninitialized/default state.</summary>
    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("UserId is not initialized (default value is invalid).");
        }
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UserId id) => id.Value;
    public static explicit operator UserId(Guid value) => new(value);
}
