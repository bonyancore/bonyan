using System.Net.Mime;
using Construction.Kernel.Tenancy;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Infrastructure.Middleware;

/// <summary>
/// Enforces Spec 01 §6.1 BR-PLT-09:
/// All requests to a suspended tenant return 403 platform.tenant_suspended,
/// with the explicit exception of GET /auth/me (and pre-auth endpoints).
/// </summary>
public sealed class TenantSuspensionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantSuspensionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Bypass unauthenticated requests (login, refresh, dev, etc.)
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // 2. Bypass explicit exception: GET /api/v1/platform/auth/me per BR-PLT-09
        if (HttpMethods.IsGet(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/api/v1/platform/auth/me", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 3. Resolve tenant ID from authenticated claims
        var tenantClaim = context.User.FindFirst("tid")?.Value ?? context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId) && tenantId != Guid.Empty)
        {
            var dbContext = context.RequestServices.GetRequiredService<PlatformDbContext>();
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, context.RequestAborted);

            if (tenant != null && tenant.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "platform.tenant_suspended",
                    Detail = "Tenant workspace is suspended.",
                    Type = "https://errors.constructionconnect.example/platform/tenant_suspended"
                };
                problem.Extensions["code"] = "platform.tenant_suspended";

                await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
