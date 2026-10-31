using Xunit;

namespace Construction.Kernel.Tests;

public class ErrorTests
{
    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);
        Assert.Equal(ErrorType.Failure, Error.None.Type);
    }

    [Fact]
    public void Failure_SetsCorrectProperties()
    {
        var err = Error.Failure("err.code", "Error message");
        Assert.Equal("err.code", err.Code);
        Assert.Equal("Error message", err.Message);
        Assert.Equal(ErrorType.Failure, err.Type);
    }

    [Fact]
    public void Validation_SetsCorrectProperties()
    {
        var err = Error.Validation("val.code", "Validation message");
        Assert.Equal("val.code", err.Code);
        Assert.Equal("Validation message", err.Message);
        Assert.Equal(ErrorType.Validation, err.Type);
    }

    [Fact]
    public void NotFound_SetsCorrectProperties()
    {
        var err = Error.NotFound("nf.code", "Not found message");
        Assert.Equal("nf.code", err.Code);
        Assert.Equal("Not found message", err.Message);
        Assert.Equal(ErrorType.NotFound, err.Type);
    }

    [Fact]
    public void Conflict_SetsCorrectProperties()
    {
        var err = Error.Conflict("conflict.code", "Conflict message");
        Assert.Equal("conflict.code", err.Code);
        Assert.Equal("Conflict message", err.Message);
        Assert.Equal(ErrorType.Conflict, err.Type);
    }

    [Fact]
    public void Unauthorized_SetsCorrectProperties()
    {
        var err = Error.Unauthorized("unauth.code", "Unauthorized message");
        Assert.Equal("unauth.code", err.Code);
        Assert.Equal("Unauthorized message", err.Message);
        Assert.Equal(ErrorType.Unauthorized, err.Type);
    }

    [Fact]
    public void Forbidden_SetsCorrectProperties()
    {
        var err = Error.Forbidden("forbid.code", "Forbidden message");
        Assert.Equal("forbid.code", err.Code);
        Assert.Equal("Forbidden message", err.Message);
        Assert.Equal(ErrorType.Forbidden, err.Type);
    }
}

