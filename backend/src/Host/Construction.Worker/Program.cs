using Construction.Kernel.Tenancy;
using Construction.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Ambient tenant is set per job by JobRunner; default fails closed.
builder.Services.AddSingleton<AsyncLocalTenantContext>();
builder.Services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());
builder.Services.AddSingleton<ITenantSetter>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());

builder.Services.AddAppModules(builder.Configuration);

await builder.Build().RunAsync();
