using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Xunit;

namespace Construction.Platform.Domain.Tests.Messaging;

public sealed class IdempotentConsumerTests
{
    [EventType("test.item.processed.v1")]
    public sealed record ItemProcessedEvent(Guid ItemId, string Status);

    private sealed class TestConsumer : IdempotentConsumer<ItemProcessedEvent>
    {
        private readonly ITenantContext _tenantContext;
        public int HandlerInvocationCount { get; private set; }
        public Guid? LastSeenTenantInHandler { get; private set; }
        public bool ShouldThrow { get; set; }

        public TestConsumer(
            IProcessedEventStore store,
            ITenantSetter tenantSetter,
            ITenantContext tenantContext)
            : base(store, tenantSetter)
        {
            _tenantContext = tenantContext;
        }

        public override string ConsumerName => "TestConsumer";

        protected override Task HandleInTransactionAsync(
            EventEnvelope<ItemProcessedEvent> envelope,
            IConsumerTransactionContext context,
            CancellationToken cancellationToken)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated handler failure.");
            }

            HandlerInvocationCount++;
            LastSeenTenantInHandler = _tenantContext.HasTenant ? _tenantContext.Current.Value : null;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleAsync_FirstDelivery_ExecutesHandlerAndRecordsEvent()
    {
        var store = new InMemoryProcessedEventStore();
        var tenantContext = new AsyncLocalTenantContext();
        var consumer = new TestConsumer(store, tenantContext, tenantContext);

        var tenantId = Guid.NewGuid();
        var payload = new ItemProcessedEvent(Guid.NewGuid(), "Ready");
        var envelope = EventEnvelope<ItemProcessedEvent>.Create(payload, tenantId);

        await consumer.HandleAsync(envelope);

        Assert.Equal(1, consumer.HandlerInvocationCount);
        Assert.Equal(tenantId, consumer.LastSeenTenantInHandler);
        Assert.True(store.IsProcessed("TestConsumer", envelope.EventId));
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_SkippedAndHandlerNotInvokedAgain()
    {
        var store = new InMemoryProcessedEventStore();
        var tenantContext = new AsyncLocalTenantContext();
        var consumer = new TestConsumer(store, tenantContext, tenantContext);

        var tenantId = Guid.NewGuid();
        var payload = new ItemProcessedEvent(Guid.NewGuid(), "Ready");
        var envelope = EventEnvelope<ItemProcessedEvent>.Create(payload, tenantId);

        // First delivery: processes normally
        await consumer.HandleAsync(envelope);
        Assert.Equal(1, consumer.HandlerInvocationCount);

        // Second delivery of same event: must be skipped
        await consumer.HandleAsync(envelope);

        Assert.Equal(1, consumer.HandlerInvocationCount); // Unchanged!
        Assert.True(store.IsProcessed("TestConsumer", envelope.EventId));
    }

    [Fact]
    public async Task HandleAsync_HandlerException_RowNotKeptAndTransactionRolledBack()
    {
        var store = new InMemoryProcessedEventStore();
        var tenantContext = new AsyncLocalTenantContext();
        var consumer = new TestConsumer(store, tenantContext, tenantContext)
        {
            ShouldThrow = true
        };

        var tenantId = Guid.NewGuid();
        var payload = new ItemProcessedEvent(Guid.NewGuid(), "Broken");
        var envelope = EventEnvelope<ItemProcessedEvent>.Create(payload, tenantId);

        // Handler throws: exception propagates (base class must not swallow it)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.HandleAsync(envelope));

        // Row must NOT be kept in processed_events (transaction rolled back)
        Assert.False(store.IsProcessed("TestConsumer", envelope.EventId));

        // Now handler is fixed: subsequent retry can process successfully
        consumer.ShouldThrow = false;
        await consumer.HandleAsync(envelope);

        Assert.Equal(1, consumer.HandlerInvocationCount);
        Assert.True(store.IsProcessed("TestConsumer", envelope.EventId));
    }

    [Fact]
    public async Task HandleAsync_MissingTenantId_RejectedWithException()
    {
        var store = new InMemoryProcessedEventStore();
        var tenantContext = new AsyncLocalTenantContext();
        var consumer = new TestConsumer(store, tenantContext, tenantContext);

        var payload = new ItemProcessedEvent(Guid.NewGuid(), "NoTenant");
        // Create an envelope manually with empty Guid for TenantId
        var invalidEnvelope = new EventEnvelope<ItemProcessedEvent>(
            EventId: Guid.NewGuid(),
            Type: "test.item.processed.v1",
            OccurredAt: DateTimeOffset.UtcNow,
            TenantId: Guid.Empty,
            ProjectId: null,
            ActorId: null,
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            Payload: payload);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.HandleAsync(invalidEnvelope));

        Assert.Equal(0, consumer.HandlerInvocationCount);
        Assert.False(store.IsProcessed("TestConsumer", invalidEnvelope.EventId));
    }
}
