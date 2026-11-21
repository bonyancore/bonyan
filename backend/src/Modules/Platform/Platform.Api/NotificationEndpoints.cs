using System.Security.Claims;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

/// <summary>PLT-209 inbox, preferences and template administration (spec 01 §7.9).</summary>
public static class NotificationEndpoints
{
    public const string ViewPermission = "platform.notification.view";
    public const string ManagePermission = "platform.notification.manage";

    public static IServiceCollection AddNotificationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ViewPermission, Policy(ViewPermission))
            .AddPolicy(ManagePermission, Policy(ManagePermission));

        return services;
    }

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/notifications")
            .WithTags("Platform Notifications");

        group.MapGet("/inbox", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                bool? unreadOnly,
                bool? includeArchived,
                DateTimeOffset? cursorCreatedAt,
                Guid? cursorId,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
                {
                    return Results.Forbid();
                }

                var size = pageSize ?? 50;
                if (size is < 1 or > 100 || cursorCreatedAt.HasValue != cursorId.HasValue)
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.InvalidPagination,
                        "pageSize must be 1-100 and both cursors supplied together."));
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var items = await reader.InboxAsync(
                    userId, unreadOnly ?? false, includeArchived ?? false,
                    cursorCreatedAt, cursorId, size, cancellationToken);

                var hasMore = items.Count >= size;
                return Results.Ok(new
                {
                    items,
                    hasMore,
                    nextCursorCreatedAt = hasMore && items.Count > 0 ? items[^1].CreatedAt : (DateTimeOffset?)null,
                    nextCursorId = hasMore && items.Count > 0 ? items[^1].Id : (Guid?)null
                });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("GetNotificationInbox")
            .WithSummary("Current user's inbox with keyset pagination")
            .WithDescription("Only the recipient's rows are returned. Requires platform.notification.view.");

        group.MapGet("/inbox/unread-count", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { unreadCount = await reader.UnreadCountAsync(userId, cancellationToken) });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("GetNotificationUnreadCount")
            .WithSummary("Unread in-app notification count")
            .WithDescription("Requires platform.notification.view.");

        group.MapPost("/inbox/{id:guid}/read", (HttpContext httpContext, INotificationReader reader,
                ITenantSetter tenantSetter, Guid id, CancellationToken cancellationToken) =>
            Mark(httpContext, reader, tenantSetter, id, archive: false, cancellationToken))
            .RequireAuthorization(ViewPermission)
            .WithName("MarkNotificationRead")
            .WithSummary("Mark one notification read (recipient only)");

        group.MapPost("/inbox/{id:guid}/archive", (HttpContext httpContext, INotificationReader reader,
                ITenantSetter tenantSetter, Guid id, CancellationToken cancellationToken) =>
            Mark(httpContext, reader, tenantSetter, id, archive: true, cancellationToken))
            .RequireAuthorization(ViewPermission)
            .WithName("ArchiveNotification")
            .WithSummary("Archive one notification (recipient only)");

        group.MapPost("/inbox/read-all", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { updated = await reader.MarkAllReadAsync(userId, cancellationToken) });
            })
            .RequireAuthorization(ViewPermission)
            .WithName("MarkAllNotificationsRead")
            .WithSummary("Mark all current user's notifications read");

        group.MapGet("/preferences", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { items = await reader.GetPreferencesAsync(userId, cancellationToken) });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("GetNotificationPreferences")
            .WithSummary("Current user's notification preferences");

        group.MapPut("/preferences", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                UpdateNotificationPreferenceRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await reader.UpsertPreferenceAsync(userId, request, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("UpdateNotificationPreference")
            .WithSummary("Update the current user's preference for one category and channel")
            .WithDescription("Mandatory approval and security channels cannot be disabled server-side.");

        group.MapGet("/templates", async (
                HttpContext httpContext,
                INotificationReader reader,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { items = await reader.ListTemplatesAsync(cancellationToken) });
            })
            .RequireAuthorization(ManagePermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("ListNotificationTemplates")
            .WithSummary("List visible templates (global defaults and tenant overrides)")
            .WithDescription("Requires platform.notification.manage.");

        return endpoints;
    }

    private static async Task<IResult> Mark(
        HttpContext httpContext,
        INotificationReader reader,
        ITenantSetter tenantSetter,
        Guid id,
        bool archive,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(httpContext.User, out var tenantId, out var userId))
        {
            return Results.Forbid();
        }

        using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
        var result = await reader.MarkReadAsync(id, userId, archive, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : PlatformApiResults.FromError(result.Error);
    }

    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> Policy(string permission) =>
        policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission);

    private static bool TryGetActor(ClaimsPrincipal principal, out Guid tenantId, out Guid userId)
    {
        tenantId = Guid.Empty;
        userId = Guid.Empty;
        var tenant = principal.FindFirst("tid") ?? principal.FindFirst("tenant_id");
        var actor = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        return Guid.TryParse(tenant?.Value, out tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(actor?.Value, out userId) && userId != Guid.Empty;
    }
}
