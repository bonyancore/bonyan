using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Construction.Web.Idempotency;

/// <summary>
/// Idempotency-Key middleware (PLT-205). Runs after authorization so a replay is still permission
/// checked, and stores the response in the same request lifecycle as the business action. Responses
/// are stored only for non-5xx outcomes; 5xx and exceptions release the claim so a retry executes.
/// </summary>
public sealed partial class IdempotencyMiddleware(
    RequestDelegate next,
    IdempotencyOptions? options = null,
    ILogger<IdempotencyMiddleware>? logger = null)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeaderName = "Idempotent-Replay";

    private static readonly string[] StoredHeaders = ["Location", "ETag", "Content-Type"];

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly IdempotencyOptions _options = options ?? new IdempotencyOptions();
    private readonly ILogger<IdempotencyMiddleware> _logger =
        logger ?? NullLogger<IdempotencyMiddleware>.Instance;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldApply(context, out var required))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var store = context.RequestServices.GetService<IIdempotencyStore>();
        if (store is null)
        {
            // No store registered (e.g. a host that has not opted in): behave as before.
            await _next(context).ConfigureAwait(false);
            return;
        }

        var key = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (required)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "platform.idempotency_key_required",
                    "An Idempotency-Key header is required for this request.").ConfigureAwait(false);
                return;
            }

            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!IsValidKey(key))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "platform.idempotency_key_invalid",
                "The Idempotency-Key header is invalid: 8-64 characters from A-Z a-z 0-9 . _ -.").ConfigureAwait(false);
            return;
        }

        if (!TryResolveScope(context, key, out var scope))
        {
            // Unauthenticated requests are handled by the auth/authorization pipeline.
            await _next(context).ConfigureAwait(false);
            return;
        }

        byte[] body;
        try
        {
            body = await ReadBodyAsync(context).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "platform.request_too_large",
                "The request body exceeds the idempotency limit.").ConfigureAwait(false);
            return;
        }

        var hash = RequestHasher.Compute(
            context.Request.Method,
            PathTemplate(context),
            body,
            context.Request.ContentType);

        var claimed = await store.TryClaimAsync(scope, hash, _options.KeyLifetime, context.RequestAborted)
            .ConfigureAwait(false);

        if (!claimed)
        {
            var existing = await store.FindAsync(scope, context.RequestAborted).ConfigureAwait(false);
            var decision = IdempotencyDecisionEngine.Decide(
                existing, hash, DateTimeOffset.UtcNow, _options.InProgressTakeover);

            if (decision.Kind == IdempotencyDecisionKind.Proceed)
            {
                var takenOver = await store.TryTakeOverAsync(
                    scope, _options.InProgressTakeover, _options.KeyLifetime, context.RequestAborted)
                    .ConfigureAwait(false);

                if (!takenOver)
                {
                    await HandleConflictAsync(context, existing).ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                await HandleDecisionAsync(context, decision).ConfigureAwait(false);
                return;
            }
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context).ConfigureAwait(false);

            var status = context.Response.StatusCode;
            var capturedHeaders = StoredHeaders
                .Where(header => context.Response.Headers.ContainsKey(header))
                .ToDictionary(
                    header => header,
                    header => context.Response.Headers[header].ToString(),
                    StringComparer.OrdinalIgnoreCase);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                await store.ReleaseAsync(scope, context.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                var tooLarge = buffer.Length > _options.MaxStoredBodyBytes;
                var payload = tooLarge ? null : buffer.ToArray();

                await store.CompleteAsync(
                    scope,
                    status,
                    context.Response.ContentType,
                    payload,
                    capturedHeaders,
                    tooLarge,
                    context.RequestAborted).ConfigureAwait(false);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            if (context.Response.Body == buffer)
            {
                context.Response.Body = originalBody;
            }

            await store.ReleaseAsync(scope, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task HandleDecisionAsync(HttpContext context, IdempotencyDecision decision)
    {
        switch (decision.Kind)
        {
            case IdempotencyDecisionKind.Replay when decision.Record is not null:
                await ReplayAsync(context, decision.Record, tooLarge: false).ConfigureAwait(false);
                break;

            case IdempotencyDecisionKind.ReplayTooLarge when decision.Record is not null:
                await ReplayAsync(context, decision.Record, tooLarge: true).ConfigureAwait(false);
                break;

            case IdempotencyDecisionKind.KeyReuse:
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    "platform.idempotency_key_reuse",
                    "The Idempotency-Key was already used with a different request.").ConfigureAwait(false);
                break;

            default:
                await HandleConflictAsync(context, decision.Record).ConfigureAwait(false);
                break;
        }
    }

    private static async Task HandleConflictAsync(HttpContext context, IdempotencyRecord? record)
    {
        if (record is { IsInProgress: true })
        {
            context.Response.Headers.RetryAfter = "2";
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "platform.request_in_progress",
                "An identical request is still in progress; retry shortly.").ConfigureAwait(false);
            return;
        }

        await WriteProblemAsync(
            context,
            StatusCodes.Status409Conflict,
            "platform.request_in_progress",
            "An identical request is still in progress; retry shortly.").ConfigureAwait(false);
    }

    private static async Task ReplayAsync(HttpContext context, IdempotencyRecord record, bool tooLarge)
    {
        if (tooLarge)
        {
            if (record.ResponseHeaders.TryGetValue("Location", out var location))
            {
                context.Response.Headers.Location = location;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "platform.idempotency_replay_too_large",
                "The stored response is too large to replay; follow the Location header.").ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = record.ResponseStatus ?? StatusCodes.Status200OK;
        context.Response.Headers[ReplayHeaderName] = "true";

        foreach (var header in record.ResponseHeaders)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = header.Value;
            }
            else if (!header.Key.Equals("ETag", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers[header.Key] = header.Value;
            }
        }

        if (record.ResponseBody is { Length: > 0 })
        {
            await context.Response.Body.WriteAsync(record.ResponseBody, context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    private bool ShouldApply(HttpContext context, out bool required)
    {
        required = false;

        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyMetadata>();
        var mode = metadata?.Mode ?? IdempotencyMode.Optional;

        if (mode == IdempotencyMode.Disabled)
        {
            return false;
        }

        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var client = context.Request.Headers[_options.ClientHeader].ToString();
        var isMobileOrPortal = client.Equals("mobile", StringComparison.OrdinalIgnoreCase)
            || client.Equals("portal", StringComparison.OrdinalIgnoreCase);

        required = mode == IdempotencyMode.Required || isMobileOrPortal;
        return true;
    }

    private async Task<byte[]> ReadBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)
                   .ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > _options.MaxRequestBodyBytes)
            {
                throw new InvalidOperationException("Request body exceeds the idempotency limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        context.Request.Body.Position = 0;
        return buffer.ToArray();
    }

    private static bool TryResolveScope(HttpContext context, string key, out IdempotencyScope scope)
    {
        scope = null!;

        var user = context.User;
        var tenantClaim = user.FindFirst("tid")?.Value ?? user.FindFirst("tenant_id")?.Value;
        var actorClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty ||
            !Guid.TryParse(actorClaim, out var actorId) || actorId == Guid.Empty)
        {
            return false;
        }

        scope = new IdempotencyScope(
            tenantId,
            actorId,
            key,
            context.Request.Method.ToUpperInvariant(),
            PathTemplate(context));

        return true;
    }

    private static string PathTemplate(HttpContext context)
    {
        var route = context.GetEndpoint() as RouteEndpoint;
        return route?.RoutePattern.RawText ?? context.Request.Path.Value ?? "/";
    }

    private static bool IsValidKey(string key) =>
        key.Length is >= 8 and <= 64 && IdempotencyKeyPattern().IsMatch(key);

    private static async Task WriteProblemAsync(HttpContext context, int status, string code, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Type = "https://construction.example.com/errors/" + code.Replace('.', '-')
        };
        problem.Extensions["code"] = code;

        await Results.Problem(problem).ExecuteAsync(context).ConfigureAwait(false);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyPattern();
}
