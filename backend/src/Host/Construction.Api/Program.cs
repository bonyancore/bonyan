using System;
using System.Threading;
using System.Threading.Tasks;
using Construction.Api;
using Construction.Api.Tenancy;
using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Platform.Core.Application.Outbox;
using Construction.Platform.Infrastructure.Application;
using Construction.Web;
using Construction.Web.Errors;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, ClaimsPrincipalTenantContext>();
builder.Services.AddSingleton<AsyncLocalTenantContext>();
builder.Services.AddSingleton<ITenantSetter>(sp => sp.GetRequiredService<AsyncLocalTenantContext>());

// Test consumer and test probe
builder.Services.AddSingleton<MessagingTestProbe>();
builder.Services.AddScoped<TestConsumer>();

builder.Services.AddWebInfrastructure(builder.Configuration);
builder.Services.AddAppModules(builder.Configuration);

// Authorization-protected endpoints need a scheme to challenge with; without one every
// RequireAuthorization endpoint throws instead of returning 401/403. The handler only authenticates
// in Development and fails closed otherwise until the real bearer scheme (auth ticket) lands.
builder.Services.AddAuthentication(DevHeaderAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(
        DevHeaderAuthenticationHandler.SchemeName,
        _ => { });

// Use InMemory store only in Development
if (builder.Environment.IsDevelopment())
{
    builder.Services.RemoveAll<IProcessedEventStore>();
    builder.Services.AddSingleton<IProcessedEventStore, InMemoryProcessedEventStore>();

    // Same Development-only rule for the dead-letter admin store (PLT-204): the PostgreSQL store stays
    // the production default, while a local run can exercise list/replay/discard/409 without a database.
    builder.Services.RemoveAll<IDeadLetterStore>();
    builder.Services.RemoveAll<IOutboxMetricsStore>();
    builder.Services.AddSingleton<InMemoryDeadLetterStore>();
    builder.Services.AddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<InMemoryDeadLetterStore>());
    builder.Services.AddSingleton<IOutboxMetricsStore>(sp => sp.GetRequiredService<InMemoryDeadLetterStore>());

    // Same Development-only rule for the idempotency store (PLT-205).
    builder.Services.RemoveAll<Construction.Web.Idempotency.IIdempotencyStore>();
    builder.Services.AddSingleton<Construction.Web.Idempotency.IIdempotencyStore,
        Construction.Web.Idempotency.InMemoryIdempotencyStore>();

    // Development-only file/notification adapters: no provider credentials exist locally, and the
    // production defaults stay fail-closed.
    builder.Services.RemoveAll<Construction.Platform.Core.Application.Notifications.INotificationRecipientResolver>();
    builder.Services.AddSingleton<Construction.Platform.Core.Application.Notifications.INotificationRecipientResolver,
        Construction.Platform.Infrastructure.Notifications.FakeNotificationRecipientResolver>();
    builder.Services.RemoveAll<Construction.Platform.Core.Application.Approvals.IApproverDirectory>();
    builder.Services.AddScoped<Construction.Platform.Core.Application.Approvals.IApproverDirectory,
        Construction.Platform.Infrastructure.Approvals.FakeApproverDirectory>();
    builder.Services.RemoveAll<Construction.Platform.Core.Application.Files.IObjectStorage>();
    builder.Services.AddSingleton<Construction.Platform.Core.Application.Files.IObjectStorage,
        Construction.Platform.Infrastructure.Files.InMemoryObjectStorage>();
    builder.Services.RemoveAll<Construction.Platform.Core.Application.Files.IFileAccessChecker>();
    builder.Services.AddSingleton<Construction.Platform.Core.Application.Files.IFileAccessChecker,
        Construction.Platform.Infrastructure.Files.FakeFileAccessChecker>();
}

// OpenAPI configuration
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

app.UseWebExceptionHandler();

// Authentication must run before the rate limiter: the platform admin policies partition per user, and
// the partition key reads HttpContext.User, which is empty until authentication has populated it.
// Limiting before authorization also rejects an abusive caller before permission checks run.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// PLT-205: idempotency runs after authorization so a replay is re-authorised, and after the rate
// limiter so duplicate floods are throttled before they touch the key store.
app.UseMiddleware<Construction.Web.Idempotency.IdempotencyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Construction API v1");
        ui.RoutePrefix = "swagger";
    });

    // PLT-201 Messaging test endpoint
    app.MapPost("/dev/messaging-test",
        async (
            MessagingTestRequest request,
            TestConsumer consumer,
            MessagingTestProbe probe,
            CancellationToken cancellationToken) =>
        {
            if (request.EventId == Guid.Empty)
            {
                return Results.BadRequest(new
                {
                    error = "EventId must not be empty."
                });
            }

            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new
                {
                    error = "TenantId must not be empty."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new
                {
                    error = "Message must not be empty."
                });
            }

            var envelope = new EventEnvelope<MessagingTestPayload>(
                EventId: request.EventId,
                Type: "plt201.test",
                OccurredAt: DateTimeOffset.UtcNow,
                TenantId: request.TenantId,
                ProjectId: null,
                ActorId: null,
                CorrelationId: request.EventId,
                CausationId: null,
                Payload: new MessagingTestPayload(request.Message));

            var countBefore = probe.GetCount(request.EventId);

            await consumer.HandleAsync(
                envelope,
                cancellationToken);

            var countAfter = probe.GetCount(request.EventId);

            var processedNow = countAfter > countBefore;

            return Results.Ok(new
            {
                eventId = request.EventId,
                tenantId = request.TenantId,
                processedNow,
                duplicate = !processedNow,
                message = processedNow
                    ? "Event processed successfully."
                    : "Duplicate event ignored."
            });
        })
        .WithTags("PLT-201 Messaging Test")
        .WithName("TestMessagingConsumer")
        .WithSummary("Test PLT-201 message idempotency");
}

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health")
   .WithName("Health");

// Map application modules
app.MapAppModules();

await app.RunAsync();

// PLT-201 test types
namespace Construction.Api
{
    // Test event payload
    public sealed record MessagingTestPayload(string Message);

    // Test endpoint request
    public sealed record MessagingTestRequest(
        Guid EventId,
        Guid TenantId,
        string Message);

    // Tracks handler executions during the current application process
    public sealed class MessagingTestProbe
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int>
            _handledEvents = new();

        public void MarkHandled(Guid eventId)
        {
            _handledEvents.AddOrUpdate(
                eventId,
                1,
                (_, count) => count + 1);
        }

        public int GetCount(Guid eventId)
        {
            return _handledEvents.TryGetValue(
                eventId,
                out var count)
                ? count
                : 0;
        }
    }

    // Test consumer for PLT-201
    public sealed class TestConsumer
        : IdempotentConsumer<MessagingTestPayload>
    {
        private readonly MessagingTestProbe _probe;

        public TestConsumer(
            IProcessedEventStore eventStore,
            ITenantSetter tenantSetter,
            MessagingTestProbe probe)
            : base(eventStore, tenantSetter)
        {
            _probe = probe;
        }

        protected override Task HandleInTransactionAsync(
            EventEnvelope<MessagingTestPayload> envelope,
            IConsumerTransactionContext context,
            CancellationToken cancellationToken)
        {
            Console.WriteLine(
                $"PLT-201 handled event {envelope.EventId}: {envelope.Payload.Message}");

            _probe.MarkHandled(envelope.EventId);

            return Task.CompletedTask;
        }
    }
}
