using Construction.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Construction.Web.Errors;

/// <summary>
/// Maps <see cref="Error"/> (PLT-101) to RFC 7807 <see cref="ProblemDetails"/>.
/// </summary>
public static class ErrorExtensions
{
    private const string ErrorTypeBaseUri = "https://construction.example.com/errors/";

    /// <summary>
    /// Returns the HTTP status code and a populated <see cref="ProblemDetails"/> for the given error.
    /// </summary>
    public static (int StatusCode, ProblemDetails Problem) ToProblemDetails(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.Validation   => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Conflict     => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
            ErrorType.RateLimited  => StatusCodes.Status429TooManyRequests,
            ErrorType.BadRequest   => StatusCodes.Status400BadRequest,
            ErrorType.PreconditionFailed   => StatusCodes.Status412PreconditionFailed,
            ErrorType.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            _                      => StatusCodes.Status500InternalServerError,
        };

        if (error.Type == ErrorType.RateLimited)
        {
            var rateLimitProblem = new ProblemDetails
            {
                Type   = ErrorTypeBaseUri + "rate-limit-exceeded",
                Title  = Error.RateLimitExceededCode,
                Detail = error.Message,
                Status = StatusCodes.Status429TooManyRequests,
            };
            rateLimitProblem.Extensions["code"] = Error.RateLimitExceededCode;
            return (StatusCodes.Status429TooManyRequests, rateLimitProblem);
        }

        var sluggedCode = error.Code.ToLowerInvariant()
            .Replace('.', '-')
            .Replace('_', '-');

        var problem = new ProblemDetails
        {
            Type   = ErrorTypeBaseUri + sluggedCode,
            Title  = error.Type.ToString(),
            Detail = error.Message,
            Status = statusCode,
        };
        problem.Extensions["code"] = error.Code;

        return (statusCode, problem);
    }
}
