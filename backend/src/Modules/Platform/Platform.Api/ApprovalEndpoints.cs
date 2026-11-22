using System.Security.Claims;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Approvals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

/// <summary>PLT-208 one-step approval endpoints (spec 01 §7.6).</summary>
public static class ApprovalEndpoints
{
    public const string RequestPermission = "platform.approval.request";
    public const string ViewPermission = "platform.approval.view";
    public const string DecidePermission = "platform.approval.decide";
    public const string ManagePermission = "platform.approval.manage";

    public static IServiceCollection AddApprovalPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(RequestPermission, Policy(RequestPermission))
            .AddPolicy(ViewPermission, Policy(ViewPermission))
            .AddPolicy(DecidePermission, Policy(DecidePermission))
            .AddPolicy(ManagePermission, Policy(ManagePermission));

        return services;
    }

    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/approvals")
            .WithTags("Platform Approvals");

        group.MapPost("/requests", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                CreateApprovalRequestRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await approvals.RequestAsync(request, actorId, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/platform/approvals/instances/{result.Value.Id}", result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(RequestPermission)
            .WithName("CreateApprovalRequest")
            .WithSummary("Create a one-step approval request from an owner-module snapshot")
            .WithDescription(
                "The amount/currency snapshot comes from the caller; the server matches the policy and resolves approvers. Requires platform.approval.request.");

        group.MapGet("/instances/{id:guid}", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var instance = await approvals.GetAsync(id, cancellationToken);
                return instance is null
                    ? PlatformApiResults.FromError(Construction.Kernel.Error.NotFound(
                        PlatformErrorCodes.ApprovalNotFound,
                        PlatformErrorCodes.English(PlatformErrorCodes.ApprovalNotFound)))
                    : Results.Ok(instance);
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("GetApprovalInstance")
            .WithSummary("Read an approval instance and its active step")
            .WithDescription("Requires platform.approval.view.");

        MapDecision(group, "approve", approve: true);
        MapDecision(group, "reject", approve: false);

        group.MapPost("/policies/simulate", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                ApprovalSimulationRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(await approvals.SimulateAsync(request, cancellationToken));
            })
            .RequireAuthorization(ManagePermission)
            .WithName("SimulateApprovalPolicy")
            .WithSummary("Dry-run policy and approver selection (no writes)")
            .WithDescription("Requires platform.approval.manage. Writes nothing and emits no event.");

        group.MapGet("/policies", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { items = await approvals.ListPoliciesAsync(cancellationToken) });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("ListApprovalPolicies")
            .WithSummary("List tenant approval policies")
            .WithDescription("Requires platform.approval.view.");

        group.MapPost("/policies", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                CreateApprovalPolicyRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await approvals.CreatePolicyAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/platform/approvals/policies/{result.Value.Id}", result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(ManagePermission)
            .WithName("CreateApprovalPolicy")
            .WithSummary("Create an approval policy with exactly one step")
            .WithDescription("Overlapping policies with equal specificity and priority are rejected. Requires platform.approval.manage.");

        return endpoints;
    }

    private static void MapDecision(RouteGroupBuilder group, string action, bool approve)
    {
        group.MapPost($"/instances/{{id:guid}}/{action}", async (
                HttpContext httpContext,
                IApprovalService approvals,
                ITenantSetter tenantSetter,
                Guid id,
                ApprovalDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
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
                    return PlatformApiResults.FromError(new Construction.Kernel.Error(
                        PlatformErrorCodes.PreconditionFailed,
                        "The If-Match header must carry the instance row version.",
                        Construction.Kernel.ErrorType.PreconditionFailed));
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await approvals.DecideAsync(
                    id, approve, request.Comment, version, actorId, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(DecidePermission)
            .WithName(approve ? "ApproveApprovalStep" : "RejectApprovalStep")
            .WithSummary(approve
                ? "Approve the active step (requires If-Match)"
                : "Reject the active step with a reason (requires If-Match)")
            .WithDescription(
                "Segregation of duties is enforced server-side; the actor is never taken from the body. Requires platform.approval.decide.");
    }

    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> Policy(string permission) =>
        policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission);

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

    private static bool TryGetActor(ClaimsPrincipal principal, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var tenant = principal.FindFirst("tid") ?? principal.FindFirst("tenant_id");
        var actor = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        return Guid.TryParse(tenant?.Value, out tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(actor?.Value, out actorId) && actorId != Guid.Empty;
    }
}
