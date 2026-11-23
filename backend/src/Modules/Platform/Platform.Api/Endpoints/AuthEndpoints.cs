using System.Security.Claims;
using Construction.Kernel;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;
using Construction.Platform.Core.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Construction.Platform.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for Authentication & Session management (Spec 01 §7.2).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/auth")
            .WithTags("Platform Auth");

        // POST /api/v1/platform/auth/login
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            [FromServices] IIdentityService identityService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            var result = await identityService.LoginAsync(request, ip, userAgent, ct);
            if (result.IsFailure)
            {
                return MapErrorToResult(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("Login");

        // POST /api/v1/platform/auth/refresh
        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            [FromServices] IIdentityService identityService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            var result = await identityService.RefreshTokenAsync(request.RefreshToken, ip, userAgent, ct);
            if (result.IsFailure)
            {
                return MapErrorToResult(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("RefreshToken");

        // POST /api/v1/platform/auth/logout
        group.MapPost("/logout", async (
            [FromServices] IIdentityService identityService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdStr, out var userGuid))
            {
                return Results.Unauthorized();
            }

            Guid? familyId = null;
            var sidStr = user.FindFirst("sid")?.Value;
            if (Guid.TryParse(sidStr, out var parsedFamilyId))
            {
                familyId = parsedFamilyId;
            }

            var result = await identityService.LogoutAsync(UserId.From(userGuid), familyId, ct);
            if (result.IsFailure)
            {
                return MapErrorToResult(result.Error);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("Logout");

        // GET /api/v1/platform/auth/me
        group.MapGet("/me", async (
            [FromServices] IIdentityService identityService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            var tenantIdStr = user.FindFirst("tid")?.Value ?? user.FindFirst("tenant_id")?.Value;

            if (!Guid.TryParse(userIdStr, out var userGuid) || !Guid.TryParse(tenantIdStr, out var tenantGuid))
            {
                return Results.Unauthorized();
            }

            var result = await identityService.GetCurrentUserProfileAsync(UserId.From(userGuid), TenantId.From(tenantGuid), ct);
            if (result.IsFailure)
            {
                return MapErrorToResult(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser");

        return endpoints;
    }

    private static IResult MapErrorToResult(Error error)
    {
        var statusCode = error.Code switch
        {
            "platform.invalid_credentials" => StatusCodes.Status401Unauthorized,
            "platform.account_locked" => StatusCodes.Status423Locked,
            "platform.tenant_suspended" => StatusCodes.Status403Forbidden,
            "platform.token_revoked" => StatusCodes.Status401Unauthorized,
            "platform.token_expired" => StatusCodes.Status401Unauthorized,
            "platform.user_not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
            Type = "https://errors.constructionconnect.example/platform/" + error.Code.Replace('.', '-')
        };
        problem.Extensions["code"] = error.Code;

        return Results.Problem(problem);
    }
}
