using Xunit;

namespace Construction.Kernel.Tests;

public class ResultTests
{
    [Fact]
    public void Success_NonGeneric_SetsIsSuccessTrueAndErrorNone()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_NonGeneric_SetsIsSuccessFalseAndStoresError()
    {
        var error = Error.Failure("test.error", "Test failure message");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_Generic_StoresValueAndHasNoError()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_Generic_ThrowsOnValueAccess()
    {
        var error = Error.NotFound("item.not_found", "Item was not found");
        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        Assert.Equal("Cannot read Value of a failed result.", ex.Message);
    }

    private sealed class TestResult : Result
    {
        public TestResult(bool isSuccess, Error error) : base(isSuccess, error) { }
    }

    [Fact]
    public void Constructor_SuccessWithNonNoneError_ThrowsInvalidOperationException()
    {
        var error = Error.Failure("code", "msg");
        var ex = Assert.Throws<InvalidOperationException>(() => new TestResult(true, error));
        Assert.Equal("A successful result cannot carry an error.", ex.Message);
    }

    [Fact]
    public void Constructor_FailureWithErrorNone_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new TestResult(false, Error.None));
        Assert.Equal("A failed result must carry an error.", ex.Message);
    }
}
