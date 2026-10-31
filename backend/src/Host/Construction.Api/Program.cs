using Construction.Api;
using Construction.Api.Tenancy;
using Construction.Kernel.Tenancy;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, ClaimsPrincipalTenantContext>();
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
