using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Construction.Platform.Core.Domain.Messaging;
using Construction.Platform.Infrastructure.Jobs.Outbox;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

public sealed class OutboxEndToEndTests
{
    [EventType("core.test_project.created.v1")]
    public sealed record TestProjectCreated(Guid ProjectId, string ProjectName);

    public sealed class TestProjectCreatedHandler(
        IProcessedEventStore processedStore,
        List<Guid> effectRecorder)
        : IdempotentConsumer<TestProjectCreated>(processedStore, new AsyncLocalTenantContext())
    {
        public override string ConsumerName => "test_project_consumer";

        protected override Task HandleInTransactionAsync(
            EventEnvelope<TestProjectCreated> envelope,
            IConsumerTransactionContext context,
            CancellationToken cancellationToken)
        {
            effectRecorder.Add(envelope.Payload.ProjectId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EndToEnd_Publish_Claim_Deliver_IdempotentConsumer_SingleEffect()
    {
        // 1. Setup DI container
        var services = new ServiceCollection();
        var tenantId = TenantId.New();
        var tenantContext = new AsyncLocalTenantContext();
        using var scope = tenantContext.BeginScope(tenantId);

        var recordedEffects = new List<Guid>();
        var processedStore = new InMemoryProcessedEventStore();

        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddSingleton<ITenantSetter>(tenantContext);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IProcessedEventStore>(processedStore);
        services.AddSingleton(recordedEffects);
        services.AddSingleton<IEventHandler<TestProjectCreated>, TestProjectCreatedHandler>();

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"outbox_e2e_{Guid.NewGuid():N}")
            .Options;
        services.AddTransient(_ => new PlatformDbContext(options, tenantContext));

        var sp = services.BuildServiceProvider();

        // 2. Setup Consumer Registry
        var consumerRegistry = new CompositeOutboxConsumerRegistry(sp);
        consumerRegistry.RegisterEventType("core.test_project.created.v1", typeof(TestProjectCreated));

        // 3. Stage outbox message using EfOutboxWriter in DbContext
        var projectId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var testEvent = new TestProjectCreated(projectId, "Bridge Alpha");

        using (var db = sp.GetRequiredService<PlatformDbContext>())
        {
            var outboxWriter = new EfOutboxWriter(db, tenantContext);
            await outboxWriter.PublishAsync(
                testEvent,
                projectId: projectId,
                actorId: actorId,
                correlationId: correlationId);

            await db.SaveChangesAsync();
        }

        // 4. Verify outbox message staged in DB with full metadata
        OutboxMessage staged;
        using (var db = sp.GetRequiredService<PlatformDbContext>())
        {
            staged = await db.Outbox.SingleAsync();
            Assert.Equal("core.test_project.created.v1", staged.Type);
            Assert.Equal(tenantId.Value, staged.TenantId);
            Assert.Equal(projectId, staged.ProjectId);
            Assert.Equal(actorId, staged.ActorId);
            Assert.Equal(correlationId, staged.CorrelationId);
            Assert.Null(staged.DispatchedAt);
        }

        // 5. Setup InMemoryOutboxRepository with the staged message
        var outboxRepo = new InMemoryOutboxRepository();
        outboxRepo.Add(staged);

        // 6. Dispatch via OutboxDispatcher using the real Consumer Registry
        var dispatcher = new OutboxDispatcher(
            outboxRepo,
            tenantContext,
            consumerRegistry.DeliverAsync,
            workerId: "test-worker-1");

        var processedCount = await dispatcher.ProcessBatchAsync();

        // 7. Assertions:
        // - exactly 1 message processed
        Assert.Equal(1, processedCount);

        // - consumer handler ran and recorded the effect
        Assert.Single(recordedEffects);
        Assert.Equal(projectId, recordedEffects[0]);

        // - message marked dispatched in repository
        var dispatched = outboxRepo.GetAllMessages().Single();
        Assert.NotNull(dispatched.DispatchedAt);

        // 8. Test Idempotency: Re-deliver the same message (e.g. at-least-once transport retry)
        // Handler should suppress duplicate execution via processed_events store
        await consumerRegistry.DeliverAsync(staged);

        // Effect count must remain exactly 1
        Assert.Single(recordedEffects);
    }
}
