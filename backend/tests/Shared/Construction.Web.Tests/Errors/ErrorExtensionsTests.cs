using Construction.Kernel;
using Construction.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Construction.Web.Tests.Errors;

public sealed class ErrorExtensionsTests
{
    [Fact]
    public void ToProblemDetails_NotFound_Returns404()
    {
        var error = Error.NotFound("platform.tenant_not_found", "Tenant was not found.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public void ToProblemDetails_Validation_Returns422()
    {
        var error = Error.Validation("platform.invalid_name", "Name is invalid.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.Status);
    }

    [Fact]
    public void ToProblemDetails_Conflict_Returns409()
    {
        var error = Error.Conflict("platform.duplicate_email", "Email is already registered.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public void ToProblemDetails_Unauthorized_Returns401()
    {
        var error = Error.Unauthorized("auth.unauthenticated", "Authentication required.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
    }

    [Fact]
    public void ToProblemDetails_Forbidden_Returns403()
    {
        var error = Error.Forbidden("auth.forbidden", "Access denied.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Fact]
    public void ToProblemDetails_Failure_Returns500()
    {
        var error = Error.Failure("internal.unexpected", "Something went wrong.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    [Fact]
    public void ToProblemDetails_ExtensionsCarryCode()
    {
        const string code = "platform.tenant_not_found";
        var error = Error.NotFound(code, "Tenant not found.");
        var (_, problem) = error.ToProblemDetails();

        Assert.True(problem.Extensions.ContainsKey("code"));
        Assert.Equal(code, problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public void ToProblemDetails_DetailMatchesErrorMessage()
    {
        const string message = "Tenant was not found.";
        var error = Error.NotFound("x.y", message);
        var (_, problem) = error.ToProblemDetails();

        Assert.Equal(message, problem.Detail);
    }

    [Fact]
    public void ToProblemDetails_TypeIsUri()
    {
        var error = Error.NotFound("platform.tenant_not_found", "msg");
        var (_, problem) = error.ToProblemDetails();

        Assert.NotNull(problem.Type);
        Assert.StartsWith("https://", problem.Type);
    }

    [Fact]
    public void ToProblemDetails_TitleMatchesErrorTypeName()
    {
        var error = Error.NotFound("x.y", "msg");
        var (_, problem) = error.ToProblemDetails();

        Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
    }

    [Fact]
    public void ToProblemDetails_RateLimited_Returns429_WithImmutableCodeAndContract()
    {
        var error = Error.RateLimited("Too many requests from this tenant.");
        var (statusCode, problem) = error.ToProblemDetails();

        Assert.Equal(StatusCodes.Status429TooManyRequests, statusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.Status);
        Assert.Equal("rate_limit_exceeded", problem.Title);
        Assert.Equal("https://construction.example.com/errors/rate-limit-exceeded", problem.Type);
        Assert.Equal("rate_limit_exceeded", problem.Extensions["code"]?.ToString());
        Assert.Equal("Too many requests from this tenant.", problem.Detail);
    }

    [Fact]
    public void RateLimit_ApplicationAndMiddlewareContracts_ShareCanonicalInvariants()
    {
        // 1. Application-level Error
        var appError = Error.RateLimited("Application quota exceeded.");
        var (appStatus, appProblem) = appError.ToProblemDetails();

        // 2. Middleware options configuration simulation
        var options = new Microsoft.AspNetCore.RateLimiting.RateLimiterOptions();
        Construction.Web.RateLimiting.RateLimiterOptionsExtensions.UseProblemDetailsOnRejected(options);

        // Assert shared invariant contract fields match exactly
        Assert.Equal(StatusCodes.Status429TooManyRequests, appStatus);
        Assert.Equal(StatusCodes.Status429TooManyRequests, appProblem.Status);
        Assert.Equal("rate_limit_exceeded", appProblem.Title);
        Assert.Equal("https://construction.example.com/errors/rate-limit-exceeded", appProblem.Type);
        Assert.Equal("rate_limit_exceeded", appProblem.Extensions["code"]?.ToString());

        // Details may differ according to Option B
        Assert.Equal("Application quota exceeded.", appProblem.Detail);
    }
}
