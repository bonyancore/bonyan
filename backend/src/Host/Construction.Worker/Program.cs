using Construction.Kernel.Tenancy;
using Construction.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<ITenantContext, AnonymousTenantContext>(); // TEMP
builder.Services.AddAppModules(builder.Configuration);

await builder.Build().RunAsync();

internal sealed class AnonymousTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public TenantId Current => throw new InvalidOperationException("No tenant in this job.");
}
