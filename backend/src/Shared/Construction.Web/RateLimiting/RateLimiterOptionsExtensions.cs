using System.Globalization;
using System.Threading.RateLimiting;
using Construction.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Construction.Web.RateLimiting;

/// <summary>
/// Extension methods for configuring ASP.NET Core RateLimiter options with RFC 7807/9457 ProblemDetails.
/// </summary>
public static class RateLimiterOptionsExtensions
{
    private const string ErrorTypeBaseUri = "https://construction.example.com/errors/";

    /// <summary>
    /// Configures the <see cref="RateLimiterOptions.OnRejected"/> callback to write a consistent
    /// RFC 7807/9457 ProblemDetails response matching the application-level rate limit contract.
    /// Preserves existing callback execution, omits Retry-After header if missing or outside [1, int.MaxValue], and fails safely.
    /// </summary>
    public static RateLimiterOptions UseProblemDetailsOnRejected(
        this RateLimiterOptions options,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var existingCallback = options.OnRejected;

        options.OnRejected = async (context, ct) =>
        {
            // 1. Run existing callback first; suppress and log any exceptions
            if (existingCallback is not null)
            {
                try
                {
                    await existingCallback(context, ct);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "RateLimiter OnRejected pre-existing callback threw an exception.");
                }
            }

            // 2. Guard: if response has already started, cannot write ProblemDetails
            if (context.HttpContext.Response.HasStarted)
            {
                return;
            }

            // 3. Guard: cancellation requested
            if (ct.IsCancellationRequested)
            {
                return;
            }

            // 4. Set HTTP status code
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // 5. Retry-After header: omit if outside [1, int.MaxValue] seconds
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var secs = (long)Math.Ceiling(retryAfter.TotalSeconds);
                if (secs >= 1 && secs <= int.MaxValue)
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        secs.ToString(CultureInfo.InvariantCulture);
                }
            }

            // 6. Write RFC 7807 ProblemDetails matching the canonical rate limit contract
            var problem = new ProblemDetails
            {
                Type = ErrorTypeBaseUri + "rate-limit-exceeded",
                Title = Error.RateLimitExceededCode,
                Detail = "Too many requests. Please wait before retrying.",
                Status = StatusCodes.Status429TooManyRequests
            };
            problem.Extensions["code"] = Error.RateLimitExceededCode;

            await context.HttpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: ct);
        };

        return options;
    }
}
