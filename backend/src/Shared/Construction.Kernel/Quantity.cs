namespace Construction.Kernel;

public readonly record struct Quantity
{
    public Quantity(decimal amount, string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainRuleViolation("kernel.invalid_unit");

        Amount = amount;
        Unit = unit.Trim().ToUpperInvariant();
    }

    public decimal Amount { get; }

    /// <remarks>default(Quantity) has a null Unit; always construct through the constructor or <see cref="Zero"/>.</remarks>
    public string Unit { get; }

    public static Quantity Zero(string unit) => new(0m, unit);

    public Quantity Add(Quantity other)
    {
        EnsureSameUnit(other);
        return new Quantity(Amount + other.Amount, Unit);
    }

    public Quantity Subtract(Quantity other)
    {
        EnsureSameUnit(other);
        return new Quantity(Amount - other.Amount, Unit);
    }

    public Quantity Multiply(decimal factor) => new(Amount * factor, Unit);

    /// <summary>Banker's rounding is NOT used: quantity rounds away from zero to the given decimals.</summary>
    public Quantity Round(int decimals = 4) =>
        new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero), Unit);

    public static Quantity operator +(Quantity a, Quantity b) => a.Add(b);
    public static Quantity operator -(Quantity a, Quantity b) => a.Subtract(b);

    private void EnsureSameUnit(Quantity other)
    {
        if (Unit is null || other.Unit is null || !string.Equals(Unit, other.Unit, StringComparison.Ordinal))
            throw new DomainRuleViolation("kernel.unit_mismatch");
    }
}

