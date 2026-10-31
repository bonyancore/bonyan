using Construction.Kernel.Tenancy;
using Construction.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Ambient tenant is set per job by JobRunner; default fails closed.
builder.Services.AddSingleton<AsyncLocalTenantContext>();
builder.Services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());
builder.Services.AddSingleton<ITenantSetter>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());

// Test adapters (in-memory job storage) are refused outside Development/Test.
builder.Services.AddAppModules(
    builder.Configuration,
    allowInMemoryStores: builder.Environment.IsDevelopment()
        || builder.Environment.IsEnvironment("Test"));

await builder.Build().RunAsync();
