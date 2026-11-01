using Construction.Docs.Core.Application;
using Construction.Docs.Core.Application.Abstractions;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Construction.Docs.Api;

public static class DocsApiRegistration
{
    public static IEndpointRouteBuilder MapDocsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/docs").WithTags("Documents");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (DomainRuleViolation exception) { return Results.BadRequest(new { code = exception.Code }); }
            catch (KeyNotFoundException exception) { return Results.NotFound(new { code = exception.Message }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "request.invalid" }); }
        });

        group.MapPost("/documents", async (RegisterDocumentInput input, DocumentService service,
            ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/docs/documents", await service.RegisterAsync(tenant.Current,
                input.RecordType, input.LinkedRecordId, input.FileId, input.Title, input.Description, cancellationToken)));
        group.MapPost("/documents/{id:guid}/versions", async (Guid id, AddDocumentVersionInput input,
            DocumentService service, ITenantContext tenant, CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/docs/documents/{id}/versions", await service.AddVersionAsync(
                id, tenant.Current, input.FileId, input.Label, cancellationToken)));
        group.MapPost("/documents/{id:guid}/versions/{versionNumber:int}/approve", async (Guid id, int versionNumber,
            DocumentService service, ITenantContext tenant, CancellationToken cancellationToken) =>
        {
            await service.ApproveVersionAsync(id, versionNumber, tenant.Current, cancellationToken);
            return Results.NoContent();
        });
        group.MapGet("/documents/{id:guid}", async (Guid id, IDocumentRepository repository,
            ITenantContext tenant, CancellationToken cancellationToken) =>
        {
            var document = await repository.GetAsync(id, tenant.Current, cancellationToken);
            return document is null ? Results.NotFound() : Results.Ok(document);
        });
        return app;
    }

    public sealed record RegisterDocumentInput(string RecordType, Guid LinkedRecordId, Guid FileId, string Title, string? Description);
    public sealed record AddDocumentVersionInput(Guid FileId, string? Label);
}
