using Construction.Api;
using Construction.Kernel.Tenancy;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ITenantContext, AnonymousTenantContext>(); // TEMP until the "tid" claim reader exists
builder.Services.AddAppModules(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Construction API";
        document.Info.Version = "v1";
        document.Info.Description = "Bonyan construction ERP, modular monolith.";
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // GET /openapi/v1.json
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Construction API v1");
        ui.RoutePrefix = "swagger";         // UI at /swagger
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health")
   .WithName("Health");

app.MapAppModules();

await app.RunAsync();

internal sealed class AnonymousTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public TenantId Current => throw new InvalidOperationException("No tenant in this request.");
}
