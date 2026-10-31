using Xunit;

namespace Construction.Kernel.Tests;

public class DomainRuleViolationTests
{
    [Fact]
    public void Constructor_WithCode_SetsCodeAndMessage()
    {
        var ex = new DomainRuleViolation("rule.failed");

        Assert.Equal("rule.failed", ex.Code);
        Assert.Equal("rule.failed", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithCodeAndMessage_SetsBothProperties()
    {
        var ex = new DomainRuleViolation("rule.failed", "Detailed message");

        Assert.Equal("rule.failed", ex.Code);
        Assert.Equal("Detailed message", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithCodeMessageAndInner_SetsAll()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new DomainRuleViolation("rule.failed", "Detailed message", inner);

        Assert.Equal("rule.failed", ex.Code);
        Assert.Equal("Detailed message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}

