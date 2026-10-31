using Construction.Kernel.Tenancy;
using Construction.Persistence.Tenancy;
using Construction.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<SettableTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<SettableTenantContext>());
builder.Services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<SettableTenantContext>());
builder.Services.AddAppModules(builder.Configuration);

await builder.Build().RunAsync();
