using Construction.Kernel;
using Construction.Procurement.Core.Domain.PurchaseRequests;
using Xunit;

namespace Construction.Procurement.Domain.Tests;

public class PurchaseRequestItemTests
{
    [Fact]
    public void Constructor_WithValidValues_TrimsDescriptionAndKeepsQuantity()
    {
        var quantity = new Quantity(20m, "bag");

        var item = new PurchaseRequestItem("  Portland cement  ", quantity);

        Assert.Equal("Portland cement", item.Description);
        Assert.Equal(quantity, item.Quantity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutDescription_ThrowsDomainRuleViolation(string? description)
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            new PurchaseRequestItem(description!, new Quantity(1m, "bag")));

        Assert.Equal("procurement.purchase_request_item_description_required", exception.Code);
    }

    [Fact]
    public void Constructor_WithDefaultQuantity_ThrowsDomainRuleViolation()
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            new PurchaseRequestItem("Portland cement", default));

        Assert.Equal("procurement.purchase_request_item_unit_required", exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_ThrowsDomainRuleViolation(decimal amount)
    {
        var exception = Assert.Throws<DomainRuleViolation>(() =>
            new PurchaseRequestItem("Portland cement", new Quantity(amount, "bag")));

        Assert.Equal("procurement.purchase_request_item_quantity_must_be_positive", exception.Code);
    }
}
