using System.Security.Claims;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.NumberSeries;
using Construction.Web.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

/// <summary>PLT-206 number-series administration (spec 01 §7.8).</summary>
public static class NumberSeriesEndpoints
{
    public const string ViewPermission = "platform.number_series.view";
    public const string ManagePermission = "platform.number_series.manage";

    public static IServiceCollection AddNumberSeriesPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(ViewPermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ViewPermission))
            .AddPolicy(ManagePermission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permission", ManagePermission));

        return services;
    }

    public static IEndpointRouteBuilder MapNumberSeriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/number-series")
            .WithTags("Platform Number Series");

        group.MapGet("", async (
                HttpContext httpContext,
                INumberSeriesStore store,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenant(httpContext.User, out var tenantId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var items = await store.ListAsync(cancellationToken);
                return Results.Ok(new { items, previewIsBinding = false });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new IdempotencyMetadata(IdempotencyMode.Disabled))
            .WithName("ListNumberSeries")
            .WithSummary("List number series with a non-binding next preview")
            .WithDescription("Requires platform.number_series.view.");

        group.MapPatch("/{code}", async (
                HttpContext httpContext,
                INumberSeriesStore store,
                ITenantSetter tenantSetter,
                string code,
                UpdateNumberSeriesRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenant(httpContext.User, out var tenantId) ||
                    !TryGetActor(httpContext.User, out var actorId))
                {
                    return Results.Forbid();
                }

                var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
                if (string.IsNullOrWhiteSpace(ifMatch))
                {
                    return PlatformApiResults.FromError(new Construction.Kernel.Error(
                        PlatformErrorCodes.PreconditionRequired,
                        PlatformErrorCodes.English(PlatformErrorCodes.PreconditionRequired),
                        Construction.Kernel.ErrorType.PreconditionRequired));
                }

                if (!TryParseVersion(ifMatch, out var version))
                {
                    return PlatformApiResults.Code(
                        StatusCodes.Status400BadRequest,
                        PlatformErrorCodes.PreconditionFailed,
                        "The If-Match header must be a strong ETag containing the row version.");
                }

                if (request.Format is { Length: > 40 })
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.NumberFormatInvalid,
                        "The format is too long."));
                }

                if (request.Reason is { Length: > 500 })
                {
                    return PlatformApiResults.Code(
                        StatusCodes.Status422UnprocessableEntity,
                        PlatformErrorCodes.NumberFormatInvalid,
                        "The reason must be at most 500 characters.");
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await store.UpdateAsync(code, request, version, actorId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(ManagePermission)
            .WithMetadata(new IdempotencyMetadata(IdempotencyMode.Disabled))
            .WithName("UpdateNumberSeries")
            .WithSummary("Update a number series (format, padding, start) with If-Match")
            .WithDescription(
                "Prefix, scope, format and start are immutable after the first allocation; padding may only increase. Requires platform.number_series.manage.");

        group.MapGet("/{code}/allocations", async (
                HttpContext httpContext,
                INumberSeriesStore store,
                ITenantSetter tenantSetter,
                string code,
                DateTimeOffset? from,
                DateTimeOffset? to,
                DateTimeOffset? cursorAllocatedAt,
                Guid? cursorId,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetTenant(httpContext.User, out var tenantId))
                {
                    return Results.Forbid();
                }

                var size = pageSize ?? 50;
                if (size is < 1 or > 100)
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.InvalidPagination,
                        "pageSize must be between 1 and 100."));
                }

                if (cursorAllocatedAt.HasValue != cursorId.HasValue)
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.InvalidPagination,
                        "Both cursor values must be supplied together."));
                }

                if (from.HasValue && to.HasValue && from > to)
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.InvalidFilter,
                        "from must be before to."));
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var page = await store.ListAllocationsAsync(
                    code, from, to, cursorAllocatedAt, cursorId, size, cancellationToken);

                return Results.Ok(new
                {
                    items = page.Items,
                    hasMore = page.HasMore,
                    nextCursorAllocatedAt = page.NextCursorAllocatedAt,
                    nextCursorId = page.NextCursorId
                });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new IdempotencyMetadata(IdempotencyMode.Disabled))
            .WithName("ListNumberAllocations")
            .WithSummary("List issued numbers for a series")
            .WithDescription("Keyset pagination; the allocation log is append-only. Requires platform.number_series.view.");

        return endpoints;
    }

    private static bool TryGetTenant(ClaimsPrincipal principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var claim = principal.FindFirst("tid") ?? principal.FindFirst("tenant_id");
        return Guid.TryParse(claim?.Value, out tenantId) && tenantId != Guid.Empty;
    }

    private static bool TryGetActor(ClaimsPrincipal principal, out Guid actorId)
    {
        actorId = Guid.Empty;
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out actorId) && actorId != Guid.Empty;
    }

    private static bool TryParseVersion(string ifMatch, out int version)
    {
        var trimmed = ifMatch.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return int.TryParse(
            trimmed,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out version) && version > 0;
    }
}
