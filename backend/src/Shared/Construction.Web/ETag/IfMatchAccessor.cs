using Construction.Kernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Construction.Web.ETag;

/// <summary>
/// Explicit request-scoped marker identifying the intended concurrency target entity.
/// </summary>
public sealed record ConcurrencyTargetKey(Type ClrType, object PrimaryKeyValue);

/// <summary>
/// Reads the parsed If-Match condition and provides target registration for the current request.
/// </summary>
public interface IIfMatchAccessor
{
    IfMatchCondition Condition { get; }
    void RegisterTarget<TEntity>(object primaryKeyValue);
}

public sealed class HttpContextIfMatchAccessor(IHttpContextAccessor httpContextAccessor) : IIfMatchAccessor
{
    public const string TargetKeyItemName = "IfMatchTargetKey";
    public const string EnforcedMarkerItemName = "IfMatchEnforced";

    private IfMatchCondition? _cachedCondition;

    public IfMatchCondition Condition
    {
        get
        {
            if (_cachedCondition != null)
            {
                return _cachedCondition;
            }

            var context = httpContextAccessor.HttpContext;
            if (context == null || !context.Request.Headers.TryGetValue("If-Match", out var headerValues))
            {
                _cachedCondition = IfMatchCondition.Missing();
                return _cachedCondition;
            }

            _cachedCondition = ETagHelper.ParseIfMatch(headerValues);
            return _cachedCondition;
        }
    }

    public void RegisterTarget<TEntity>(object primaryKeyValue)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HttpContext available to register concurrency target.");

        ArgumentNullException.ThrowIfNull(primaryKeyValue);
        context.Items[TargetKeyItemName] = new ConcurrencyTargetKey(typeof(TEntity), primaryKeyValue);
    }
}

/// <summary>
/// Precondition guard evaluating an entity's current version against the parsed If-Match header.
/// </summary>
public static class PreconditionGuard
{
    /// <summary>
    /// Evaluates the If-Match condition against the loaded entity.
    /// If precondition passes, registers the target and returns null.
    /// If precondition fails, returns an RFC 7807 412 ProblemDetails result with currentVersion.
    /// </summary>
    public static IResult? EvaluatePrecondition<TEntity>(
        this IIfMatchAccessor accessor,
        TEntity entity,
        object primaryKeyValue)
        where TEntity : IConcurrencyVersioned
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(primaryKeyValue);

        var currentEtag = ETagHelper.ToETag(entity.RowVersion);

        // Evaluate wildcard: proven to exist because entity is loaded
        if (accessor.Condition.IsWildcard)
        {
            accessor.RegisterTarget<TEntity>(primaryKeyValue);
            return null; // Passes
        }

        if (accessor.Condition.Matches(currentEtag))
        {
            accessor.RegisterTarget<TEntity>(primaryKeyValue);
            return null; // Passes
        }

        // Precondition failed (mismatch or only weak tags provided for mutation)
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status412PreconditionFailed,
            Title = "precondition_failed",
            Type = "https://construction.example.com/errors/precondition-failed",
            Detail = "The resource current version does not match the supplied If-Match precondition."
        };
        problem.Extensions["code"] = "concurrency.precondition_failed";
        problem.Extensions["currentVersion"] = currentEtag;

        return Results.Problem(problem);
    }
}

/// <summary>
/// Endpoint filter extensions for If-Match enforcement.
/// </summary>
public static class IfMatchEndpointFilterExtensions
{
    private const string PreconditionRequiredType = "https://construction.example.com/errors/precondition-required";
    private const string BadRequestType = "https://construction.example.com/errors/bad-request";

    /// <summary>
    /// Adds an endpoint filter that requires a syntactically valid If-Match header.
    /// Returns 428 if missing, 400 if malformed.
    /// </summary>
    public static RouteHandlerBuilder WithIfMatchRequired(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<RequireIfMatchFilter>();

    private sealed class RequireIfMatchFilter(IIfMatchAccessor ifMatch, IHttpContextAccessor httpContextAccessor) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
            }

            var condition = ifMatch.Condition;

            if (condition.IsMissing)
            {
                return Results.Problem(
                    detail: "The If-Match header is required for this operation.",
                    statusCode: StatusCodes.Status428PreconditionRequired,
                    title: "Precondition Required",
                    type: PreconditionRequiredType);
            }

            if (condition.IsMalformed)
            {
                return Results.Problem(
                    detail: condition.MalformedReason ?? "Malformed If-Match header value.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    type: BadRequestType);
            }

            return await next(ctx);
        }
    }
}
