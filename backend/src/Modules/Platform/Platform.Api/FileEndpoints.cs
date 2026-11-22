using System.Security.Claims;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Files;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Api;

/// <summary>PLT-207 file upload/download endpoints (spec 01 §7.7).</summary>
public static class FileEndpoints
{
    public const string UploadPermission = "platform.file.upload";
    public const string ViewPermission = "platform.file.view";
    public const string DeletePermission = "platform.file.delete";
    public const string LinkPermission = "platform.file.link";

    public static IServiceCollection AddFilePolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(UploadPermission, Policy(UploadPermission))
            .AddPolicy(ViewPermission, Policy(ViewPermission))
            .AddPolicy(DeletePermission, Policy(DeletePermission))
            .AddPolicy(LinkPermission, Policy(LinkPermission));

        return services;
    }

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/files")
            .WithTags("Platform Files");

        group.MapPost("/uploads", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                CreateUploadRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await files.CreateUploadAsync(request, actorId, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/platform/files/{result.Value.FileId}", result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(UploadPermission)
            .WithName("CreateFileUpload")
            .WithSummary("Create an upload session and a short-lived pre-signed PUT URL")
            .WithDescription("Requires platform.file.upload. The storage key is server-generated.");

        group.MapPost("/{id:guid}/complete", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await files.CompleteUploadAsync(id, actorId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(UploadPermission)
            .WithName("CompleteFileUpload")
            .WithSummary("Verify the object and move the file to scanning")
            .WithDescription("Requires platform.file.upload. Completion enqueues the scan; download stays blocked.");

        group.MapGet("/{id:guid}", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var file = await files.GetAsync(id, cancellationToken);
                return file is null
                    ? PlatformApiResults.FromError(Construction.Kernel.Error.NotFound(
                        PlatformErrorCodes.FileNotFound,
                        PlatformErrorCodes.English(PlatformErrorCodes.FileNotFound)))
                    : Results.Ok(file);
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("GetFileMetadata")
            .WithSummary("Read safe file metadata")
            .WithDescription("Never returns storage keys or scanner internals. Requires platform.file.view.");

        group.MapPost("/{id:guid}/download", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await files.DownloadAsync(id, actorId, ip, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(ViewPermission)
            .WithName("DownloadFile")
            .WithSummary("Issue a five-minute pre-signed download URL")
            .WithDescription(
                "Re-checks linked-record access on every request and writes an audit row. Requires platform.file.view.");

        group.MapDelete("/{id:guid}", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await files.DeleteAsync(id, actorId, ip, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(DeletePermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("DeleteFile")
            .WithSummary("Logically delete a file")
            .WithDescription("Legal hold and posted financial links block deletion. Requires platform.file.delete.");

        group.MapPost("/{id:guid}/links", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CreateFileLinkRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out var actorId))
                {
                    return Results.Forbid();
                }

                if (string.IsNullOrWhiteSpace(request.EntityType) || request.EntityId == Guid.Empty)
                {
                    return PlatformApiResults.FromError(Construction.Kernel.Error.Validation(
                        PlatformErrorCodes.InvalidFilter, "entityType and entityId are required."));
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                var result = await files.AddLinkAsync(id, request, actorId, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/platform/files/{id}/links", result.Value)
                    : PlatformApiResults.FromError(result.Error);
            })
            .RequireAuthorization(LinkPermission)
            .WithName("LinkFile")
            .WithSummary("Attach an available file to a business record")
            .WithDescription("Requires platform.file.link. Quarantined files cannot be linked.");

        group.MapGet("/{id:guid}/links", async (
                HttpContext httpContext,
                IFileService files,
                ITenantSetter tenantSetter,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetActor(httpContext.User, out var tenantId, out _))
                {
                    return Results.Forbid();
                }

                using var scope = tenantSetter.BeginScope(new TenantId(tenantId));
                return Results.Ok(new { items = await files.ListLinksAsync(id, cancellationToken) });
            })
            .RequireAuthorization(ViewPermission)
            .WithMetadata(new Construction.Web.Idempotency.IdempotencyMetadata(
                Construction.Web.Idempotency.IdempotencyMode.Disabled))
            .WithName("ListFileLinks")
            .WithSummary("List file links")
            .WithDescription("Requires platform.file.view.");

        return endpoints;
    }

    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> Policy(string permission) =>
        policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission);

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
