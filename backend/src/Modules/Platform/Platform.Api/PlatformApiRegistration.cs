using System.Security.Claims;
using System.Threading.RateLimiting;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Application.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

public static class PlatformApiRegistration
{
    private const string AdminMutationRateLimit = "platform-outbox-admin";
    private const int MaxNoteLength = 500;
    private const int MaxEventTypeLength = 200;

    // Spec 01 §7.5 (outbox admin table) and §10.2 (permission catalogue): listing/inspecting dead
    // letters and the lag metric use `platform.outbox.view`; replay and discard use
    // `platform.outbox.manage`. The recipe's `platform.outbox.replay` does not exist in the catalogue.
    private const string ViewPermission = "platform.outbox.view";
    private const string ManagePermission = "platform.outbox.manage";

    private static readonly string[] KnownResolutions = ["open", "replayed", "discarded"];

    public static IServiceCollection AddPlatformApi(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ViewPermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ViewPermission))
            .AddPolicy(ManagePermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ManagePermission));

        // Replay and discard are the only state-changing admin operations here, so they are throttled
        // per user (E-series abuse case) rather than globally.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AdminMutationRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub")
                        ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/outbox/dead")
            .WithTags("Platform Outbox Dead Letters");

        group.MapGet("", async (
                HttpContext httpContext,
                IDeadLetterStore store,
                ITenantSetter tenantSetter,
                string? eventType,
                string? resolution,
                DateTimeOffset? cursorDeadAt,
                Guid? cursorId,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenantAndActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                var size = pageSize ?? 50;
                if (size is < 1 or > 100 || cursorDeadAt.HasValue != cursorId.HasValue)
                {
                    return Results.BadRequest(new { code = "platform.invalid_pagination" });
                }

                var typeFilter = string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim();
                if (typeFilter is { Length: > MaxEventTypeLength })
                {
                    return Results.BadRequest(new { code = "platform.invalid_filter" });
                }

                var resolutionFilter = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
                if (resolutionFilter is not null && !KnownResolutions.Contains(resolutionFilter))
                {
                    return Results.BadRequest(new { code = "platform.invalid_filter" });
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var rows = await store.ListAsync(
                    tenantId,
                    typeFilter,
                    resolutionFilter,
                    cursorDeadAt,
                    cursorId,
                    size,
                    cancellationToken);

                var hasMore = rows.Count > size;
                var items = rows.Take(size).ToArray();
                return Results.Ok(new
                {
                    items,
                    hasMore,
                    nextCursorDeadAt = hasMore && items.Length > 0 ? items[^1].DeadAt : (DateTimeOffset?)null,
                    nextCursorId = hasMore && items.Length > 0 ? items[^1].Id : (Guid?)null
                });
            })
            .RequireAuthorization(ViewPermission)
            .WithName("ListDeadOutboxMessages")
            .WithSummary("List dead-lettered outbox events")
            .WithDescription(
                "Tenant-scoped keyset pagination; filters eventType and resolution. Requires platform.outbox.view.");

        group.MapGet("/{id:guid}", async (
                HttpContext httpContext,
                IDeadLetterStore store,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenantAndActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var row = await store.GetAsync(tenantId, id, cancellationToken);
                return row is null
                    ? Results.NotFound(new { code = "platform.dead_message_not_found" })
                    : Results.Ok(row);
            })
            .RequireAuthorization(ViewPermission)
            .WithName("GetDeadOutboxMessage")
            .WithSummary("Get a dead-lettered outbox event")
            .WithDescription("Requires platform.outbox.view.");

        group.MapPost("/{id:guid}/replay", async (
                HttpContext httpContext,
                IDeadLetterStore store,
                ITenantSetter tenantSetter,
                Guid id,
                ReplayDeadOutboxRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenantAndActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }
                if (!IsValidNote(request.Reason))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["reason"] = [$"A replay reason of 1 to {MaxNoteLength} characters is required."]
                    });
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await store.ReplayAsync(tenantId, actorId, id, request.Reason, cancellationToken);
                return result.State == DeadLetterResolutionState.Resolved
                    ? Results.Ok(new ReplayDeadOutboxResponse(id, result.EventId, result.At, actorId))
                    : MutationFailure(result);
            })
            .RequireAuthorization(ManagePermission)
            .RequireRateLimiting(AdminMutationRateLimit)
            .WithName("ReplayDeadOutboxMessage")
            .WithSummary("Replay a dead-lettered outbox event")
            .WithDescription(
                "Creates a fresh outbox event and records who replayed it and why. A second replay is 409. Requires platform.outbox.manage.");

        group.MapPost("/{id:guid}/discard", async (
                HttpContext httpContext,
                IDeadLetterStore store,
                ITenantSetter tenantSetter,
                Guid id,
                DiscardDeadOutboxRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenantAndActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                // Spec 01 §4.5 / BR-PLT-63: a discard always needs a note.
                if (!IsValidNote(request.Note))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["note"] = [$"A discard note of 1 to {MaxNoteLength} characters is required."]
                    });
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await store.DiscardAsync(tenantId, actorId, id, request.Note, cancellationToken);
                return result.State == DeadLetterResolutionState.Resolved
                    ? Results.Ok(new DiscardDeadOutboxResponse(id, result.At, actorId, request.Note.Trim()))
                    : MutationFailure(result);
            })
            .RequireAuthorization(ManagePermission)
            .RequireRateLimiting(AdminMutationRateLimit)
            .WithName("DiscardDeadOutboxMessage")
            .WithSummary("Discard a dead-lettered outbox event")
            .WithDescription("Resolves the dead letter without redelivery. The note is mandatory. Requires platform.outbox.manage.");

        endpoints.MapGet("/api/v1/platform/outbox/lag", async (
                HttpContext httpContext,
                IOutboxMetricsStore store,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenantAndActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(await store.GetLagAsync(tenantId, cancellationToken));
            })
            .WithTags("Platform Outbox Dead Letters")
            .RequireAuthorization(ViewPermission)
            .WithName("GetOutboxLag")
            .WithSummary("Outbox lag for the current tenant")
            .WithDescription("Returns lagSeconds and pendingCount. Requires platform.outbox.view.");

        return endpoints;
    }

    /// <summary>404 for an unknown id, 409 when the row is no longer <c>open</c> (second replay).</summary>
    private static IResult MutationFailure(DeadLetterMutationResult result) =>
        result.State == DeadLetterResolutionState.NotFound
            ? Results.NotFound(new { code = "platform.dead_message_not_found" })
            : Results.Conflict(new { code = "dead_letter_resolved" });

    private static bool IsValidNote(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaxNoteLength;

    private static bool TryGetTenantAndActor(ClaimsPrincipal principal, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var tenantClaim = principal.FindFirstValue("tid") ?? principal.FindFirstValue("tenant_id");
        var actorClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(tenantClaim, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(actorClaim, out actorId)
            && actorId != Guid.Empty;
    }
}
