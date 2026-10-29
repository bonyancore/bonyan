namespace Construction.Kernel;

public readonly record struct Money
{
    public const string DefaultCurrency = "EGP";

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (currency is null || currency.Length != 3)
            throw new DomainRuleViolation("kernel.invalid_currency");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }

    /// <remarks>default(Money) has a null Currency; always construct through the constructor or <see cref="Zero"/>.</remarks>
    public string Currency { get; }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Banker's rounding is NOT used: money rounds away from zero to the given decimals.</summary>
    public Money Round(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero), Currency);

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new DomainRuleViolation("kernel.currency_mismatch");
    }
}
