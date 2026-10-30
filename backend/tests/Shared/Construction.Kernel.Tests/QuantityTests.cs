using Xunit;

namespace Construction.Kernel.Tests;

public class QuantityTests
{
    [Fact]
    public void Constructor_WithValidAmountAndUnit_NormalizesUnitAndAssignsProperties()
    {
        var qty = new Quantity(15.5m, "m2");

        Assert.Equal(15.5m, qty.Amount);
        Assert.Equal("M2", qty.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhitespaceUnit_ThrowsDomainRuleViolation(string? unit)
    {
        var ex = Assert.Throws<DomainRuleViolation>(() => new Quantity(10m, unit!));
        Assert.Equal("kernel.invalid_unit", ex.Code);
    }

    [Fact]
    public void Zero_ReturnsZeroAmountWithNormalizedUnit()
    {
        var zero = Quantity.Zero("kg");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("KG", zero.Unit);
    }

    [Fact]
    public void Add_WithSameUnit_ReturnsCombinedQuantity()
    {
        var a = new Quantity(10.25m, "M3");
        var b = new Quantity(5.5m, "m3");

        var result = a.Add(b);

        Assert.Equal(15.75m, result.Amount);
        Assert.Equal("M3", result.Unit);
    }

    [Fact]
    public void Add_WithDifferentUnit_ThrowsDomainRuleViolation()
    {
        var a = new Quantity(10m, "M3");
        var b = new Quantity(5m, "KG");

        var ex = Assert.Throws<DomainRuleViolation>(() => a.Add(b));
        Assert.Equal("kernel.unit_mismatch", ex.Code);
    }

    [Fact]
    public void Add_WithDefaultQuantity_ThrowsDomainRuleViolation()
    {
        var valid = new Quantity(10m, "M3");
        Quantity uninitialized = default;

        var ex1 = Assert.Throws<DomainRuleViolation>(() => valid.Add(uninitialized));
        Assert.Equal("kernel.unit_mismatch", ex1.Code);

        var ex2 = Assert.Throws<DomainRuleViolation>(() => uninitialized.Add(valid));
        Assert.Equal("kernel.unit_mismatch", ex2.Code);
    }

    [Fact]
    public void Subtract_WithSameUnit_ReturnsSubtractedQuantity()
    {
        var a = new Quantity(20m, "TON");
        var b = new Quantity(7.5m, "ton");

        var result = a.Subtract(b);

        Assert.Equal(12.5m, result.Amount);
        Assert.Equal("TON", result.Unit);
    }

    [Fact]
    public void Subtract_WithDifferentUnit_ThrowsDomainRuleViolation()
    {
        var a = new Quantity(20m, "TON");
        var b = new Quantity(5m, "M2");

        var ex = Assert.Throws<DomainRuleViolation>(() => a.Subtract(b));
        Assert.Equal("kernel.unit_mismatch", ex.Code);
    }

    [Fact]
    public void Multiply_ScalesAmountCorrectly()
    {
        var qty = new Quantity(12.5m, "LM");

        var result = qty.Multiply(2.5m);

        Assert.Equal(31.25m, result.Amount);
        Assert.Equal("LM", result.Unit);
    }

    [Fact]
    public void OperatorPlusAndMinus_BehaveIdenticallyToAddAndSubtract()
    {
        var a = new Quantity(10m, "M2");
        var b = new Quantity(4m, "M2");

        var sum = a + b;
        var diff = a - b;

        Assert.Equal(14m, sum.Amount);
        Assert.Equal(6m, diff.Amount);
    }

    [Theory]
    [InlineData(1.23456, 4, 1.2346)]
    [InlineData(1.23454, 4, 1.2345)]
    [InlineData(1.23455, 4, 1.2346)]
    [InlineData(-1.23455, 4, -1.2346)]
    [InlineData(1.5, 0, 2.0)]
    [InlineData(-1.5, 0, -2.0)]
    public void Round_RoundsAwayFromZero(decimal amount, int decimals, decimal expected)
    {
        var qty = new Quantity(amount, "M3");

        var rounded = qty.Round(decimals);

        Assert.Equal(expected, rounded.Amount);
        Assert.Equal("M3", rounded.Unit);
    }
}

