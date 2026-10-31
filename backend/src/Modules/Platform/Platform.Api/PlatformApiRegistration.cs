using System.Security.Claims;
using System.Threading.RateLimiting;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Application.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

public static class PlatformApiRegistration
{
    private const string ReplayRateLimit = "platform-outbox-replay";

    // Spec 01 §10.2 (permission catalogue) and §12 (API table): listing dead letters uses
    // `platform.outbox.view`; replay/discard use `platform.outbox.manage`. The recipe's
    // `platform.outbox.replay` code does not exist in the catalogue (verified, not assumed).
    private const string ViewPermission = "platform.outbox.view";
    private const string ManagePermission = "platform.outbox.manage";

    public static IServiceCollection AddPlatformApi(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ViewPermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ViewPermission))
            .AddPolicy(ManagePermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ManagePermission));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ReplayRateLimit, context =>
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

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var rows = await store.ListAsync(tenantId, cursorDeadAt, cursorId, size, cancellationToken);
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
            .WithDescription("Tenant-scoped keyset pagination. Requires platform.outbox.view.");

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
                return row is null ? Results.NotFound() : Results.Ok(row);
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
                if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["reason"] = ["Replay reason is required and must be at most 500 characters."]
                    });
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var replay = await store.ReplayAsync(tenantId, actorId, id, request.Reason, cancellationToken);
                return replay is null
                    ? Results.Conflict(new { code = "platform.dead_message_already_replayed_or_not_found" })
                    : Results.Ok(replay);
            })
            .RequireAuthorization(ManagePermission)
            .RequireRateLimiting(ReplayRateLimit)
            .WithName("ReplayDeadOutboxMessage")
            .WithSummary("Replay a dead-lettered outbox event")
            .WithDescription("Creates a fresh outbox event and atomically audits who replayed it and why.");

        return endpoints;
    }

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
