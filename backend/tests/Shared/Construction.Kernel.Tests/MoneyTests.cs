using Xunit;

namespace Construction.Kernel.Tests;

public class MoneyTests
{
    [Fact]
    public void Constructor_DefaultCurrency_SetsEGP()
    {
        var money = new Money(100m);

        Assert.Equal(100m, money.Amount);
        Assert.Equal("EGP", money.Currency);
    }

    [Fact]
    public void Constructor_ExplicitCurrency_NormalizesToUpper()
    {
        var money = new Money(50m, "usd");

        Assert.Equal(50m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EG")]
    [InlineData("EGPP")]
    public void Constructor_InvalidCurrency_ThrowsDomainRuleViolation(string? currency)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() => new Money(10m, currency!));
        Assert.Equal("kernel.invalid_currency", ex.Code);
    }

    [Fact]
    public void Zero_ReturnsZeroAmount()
    {
        var zero = Money.Zero("EUR");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("EUR", zero.Currency);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedAmount()
    {
        var a = new Money(100m, "USD");
        var b = new Money(50m, "usd");

        var result = a.Add(b);

        Assert.Equal(150m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Add_MismatchedCurrency_ThrowsDomainRuleViolation()
    {
        var a = new Money(100m, "USD");
        var b = new Money(50m, "EUR");

        var ex = Assert.Throws<DomainRuleViolation>(() => a.Add(b));
        Assert.Equal("kernel.currency_mismatch", ex.Code);
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsSubtractedAmount()
    {
        var a = new Money(100m, "USD");
        var b = new Money(40m, "USD");

        var result = a.Subtract(b);

        Assert.Equal(60m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Subtract_MismatchedCurrency_ThrowsDomainRuleViolation()
    {
        var a = new Money(100m, "USD");
        var b = new Money(40m, "EGP");

        var ex = Assert.Throws<DomainRuleViolation>(() => a.Subtract(b));
        Assert.Equal("kernel.currency_mismatch", ex.Code);
    }

    [Fact]
    public void Multiply_ScalesAmountCorrectly()
    {
        var money = new Money(25m, "EGP");

        var result = money.Multiply(3m);

        Assert.Equal(75m, result.Amount);
        Assert.Equal("EGP", result.Currency);
    }

    [Theory]
    [InlineData(0.125, 2, 0.13)]
    [InlineData(-0.125, 2, -0.13)]
    [InlineData(0.124, 2, 0.12)]
    [InlineData(0.126, 2, 0.13)]
    [InlineData(10.5, 0, 11)]
    [InlineData(-10.5, 0, -11)]
    public void Round_RoundsAwayFromZero(decimal amount, int decimals, decimal expected)
    {
        var money = new Money(amount, "USD");

        var rounded = money.Round(decimals);

        Assert.Equal(expected, rounded.Amount);
        Assert.Equal("USD", rounded.Currency);
    }

    [Fact]
    public void OperatorPlusAndMinus_BehaveLikeAddAndSubtract()
    {
        var a = new Money(100m, "SAR");
        var b = new Money(30m, "SAR");

        var sum = a + b;
        var diff = a - b;

        Assert.Equal(130m, sum.Amount);
        Assert.Equal(70m, diff.Amount);
        Assert.Equal("SAR", sum.Currency);
        Assert.Equal("SAR", diff.Currency);
    }
}

