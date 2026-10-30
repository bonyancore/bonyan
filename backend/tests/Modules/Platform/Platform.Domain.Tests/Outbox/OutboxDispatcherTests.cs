using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Jobs.Outbox;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

public sealed class OutboxDispatcherTests
{
    [EventType("test.order.placed.v1")]
    private sealed record OrderPlaced(Guid AggregateId, long AggregateVersion);

    private static OutboxMessage Msg(
        Guid id,
        Guid aggregateId,
        Guid? tenantId = null,
        DateTimeOffset? occurredAt = null,
        int attempts = 0)
    {
        return new OutboxMessage
        {
            Id = id,
            TenantId = tenantId ?? Guid.CreateVersion7(),
            AggregateType = "order",
            AggregateId = aggregateId,
            AggregateVersion = 1,
            Type = "test.order.placed.v1",
            Payload = """{"aggregateId":"00000000-0000-0000-0000-000000000001"}""",
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            Attempts = attempts,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
    }

    [Fact]
    public async Task ProcessBatch_DeliversAndMarksDispatched_ExactlyOnceEffect()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();
        var store = new InMemoryProcessedEventStore();
        var effectCount = 0;

        var id = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        repo.Add(Msg(id, Guid.CreateVersion7(), tenantId));

        var dispatcher = new OutboxDispatcher(
            repo,
            tenantContext,
            async (message, ct) =>
            {
                Assert.Equal(tenantId, tenantContext.Current.Value);
                await using var tx = await store.BeginTransactionAsync(ct);
                var inserted = await tx.TryRecordProcessedAsync("test-consumer", message.Id, ct);
                if (inserted)
                {
                    Interlocked.Increment(ref effectCount);
                }

                await tx.CommitAsync(ct);
            },
            workerId: "w1");

        var processed = await dispatcher.ProcessBatchAsync();

        Assert.Equal(1, processed);
        Assert.Equal(1, effectCount);
        Assert.NotNull(repo.GetAllMessages().Single().DispatchedAt);
    }

    [Fact]
    public async Task ParallelDispatchers_DoNotDoubleDeliverWhileLeaseHeld()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();
        var deliveryCount = 0;

        var messages = Enumerable.Range(0, 40)
            .Select(_ => Msg(Guid.CreateVersion7(), Guid.CreateVersion7()))
            .ToList();
        repo.AddRange(messages);

        OutboxMessageDeliveryHandler handler = (_, _) =>
        {
            Interlocked.Increment(ref deliveryCount);
            return Task.CompletedTask;
        };

        var dispatchers = Enumerable.Range(0, 8)
            .Select(i => new OutboxDispatcher(repo, tenantContext, handler, $"w{i}"))
            .ToList();

        await Task.WhenAll(dispatchers.Select(d => d.ProcessBatchAsync(batchSize: 50)));

        Assert.Equal(messages.Count, deliveryCount);
        Assert.All(repo.GetAllMessages(), m => Assert.NotNull(m.DispatchedAt));
    }

    [Fact]
    public async Task CrashAfterEffectBeforeMark_Redelivery_ProducesOneEffect()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();
        var store = new InMemoryProcessedEventStore();
        var effectCount = 0;
        var deliveries = 0;

        var id = Guid.CreateVersion7();
        repo.Add(Msg(id, Guid.CreateVersion7()));

        OutboxMessageDeliveryHandler handler = async (message, ct) =>
        {
            Interlocked.Increment(ref deliveries);
            await using var tx = await store.BeginTransactionAsync(ct);
            var inserted = await tx.TryRecordProcessedAsync("crash-consumer", message.Id, ct);
            if (inserted)
            {
                Interlocked.Increment(ref effectCount);
            }

            await tx.CommitAsync(ct);
        };

        // First pass: deliver effect, simulate crash before mark by not marking.
        var claimed = await repo.ClaimBatchAsync(1, "w-crash", TimeSpan.FromSeconds(60));
        Assert.Single(claimed);
        await handler(claimed[0], CancellationToken.None);
        // crash: skip MarkDispatchedAsync
        repo.ExpireAllLeases();

        // Second pass: another worker redelivers; idempotent consumer keeps effect once.
        var dispatcher = new OutboxDispatcher(repo, tenantContext, handler, "w-retry");
        await dispatcher.ProcessBatchAsync();

        Assert.Equal(2, deliveries);
        Assert.Equal(1, effectCount);
        Assert.NotNull(repo.GetAllMessages().Single().DispatchedAt);
    }

    [Fact]
    public async Task PerAggregateOrdering_InterleavedAggregates_ConsumedInIdOrder()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();

        var aggA = Guid.CreateVersion7();
        var aggB = Guid.CreateVersion7();
        var aggC = Guid.CreateVersion7();

        // Explicitly ordered ids (Guid.CompareTo) — same-ms UUID v7 can shuffle rand bits.
        var a1 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000a1"), aggA);
        var b1 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000b1"), aggB);
        var c1 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000c1"), aggC);
        var a2 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000a2"), aggA);
        var b2 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000b2"), aggB);
        var a3 = Msg(Guid.Parse("01900000-0000-7000-8000-0000000000a3"), aggA);

        // Insert interleaved; claim must still respect per-aggregate id order.
        repo.AddRange([a2, b2, c1, a1, b1, a3]);

        var observed = new Dictionary<Guid, List<Guid>>();
        var gate = new object();

        var dispatcher = new OutboxDispatcher(
            repo,
            tenantContext,
            (message, _) =>
            {
                lock (gate)
                {
                    if (!observed.TryGetValue(message.AggregateId, out var list))
                    {
                        list = [];
                        observed[message.AggregateId] = list;
                    }

                    list.Add(message.Id);
                }

                return Task.CompletedTask;
            },
            "w-order");

        // Drain until empty (one eligible head per aggregate per batch).
        int rounds = 0;
        while (rounds++ < 20)
        {
            var n = await dispatcher.ProcessBatchAsync(batchSize: 10);
            if (n == 0) break;
        }

        Assert.Equal([a1.Id, a2.Id, a3.Id], observed[aggA]);
        Assert.Equal([b1.Id, b2.Id], observed[aggB]);
        Assert.Equal([c1.Id], observed[aggC]);
    }

    [Fact]
    public async Task DeadLetterEarlierEvent_BlocksLaterEventsOfSameAggregate()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();

        var agg = Guid.CreateVersion7();
        var earlier = Msg(Guid.Parse("01900000-0000-7000-8000-000000000001"), agg);
        earlier.DeadAt = DateTimeOffset.UtcNow;
        var later = Msg(Guid.Parse("01900000-0000-7000-8000-000000000002"), agg);
        repo.AddRange([earlier, later]);

        var delivered = new List<Guid>();
        var dispatcher = new OutboxDispatcher(
            repo,
            tenantContext,
            (message, _) =>
            {
                delivered.Add(message.Id);
                return Task.CompletedTask;
            },
            "w-dead");

        var n = await dispatcher.ProcessBatchAsync();

        Assert.Equal(0, n);
        Assert.Empty(delivered);
    }

    [Fact]
    public async Task LagMetric_RisesWhenDispatcherStopped()
    {
        var repo = new InMemoryOutboxRepository();
        var old = DateTimeOffset.UtcNow.AddMinutes(-5);
        repo.Add(Msg(Guid.CreateVersion7(), Guid.CreateVersion7(), occurredAt: old));

        var before = await repo.GetMetricsAsync();
        Assert.True(before.LagSeconds >= 290);
        Assert.Equal(1, before.PendingCount);

        await Task.Delay(1100);

        var after = await repo.GetMetricsAsync();
        Assert.True(after.LagSeconds > before.LagSeconds);
    }

    [Fact]
    public async Task DeliveryFailure_IncrementsAttemptsAndClearsLease()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();
        var id = Guid.CreateVersion7();
        repo.Add(Msg(id, Guid.CreateVersion7()));

        var dispatcher = new OutboxDispatcher(
            repo,
            tenantContext,
            (_, _) => throw new InvalidOperationException("boom"),
            "w-fail");

        await dispatcher.ProcessBatchAsync();

        var message = repo.GetAllMessages().Single();
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.DispatchedAt);
        Assert.Null(message.LockedBy);
        Assert.Null(message.LockedUntil);
        Assert.True(message.NextAttemptAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CalculateBackoff_MatchesSchedule()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), OutboxDispatcher.CalculateBackoff(1).Delay);
        Assert.Equal(TimeSpan.FromMinutes(1), OutboxDispatcher.CalculateBackoff(2).Delay);
        Assert.Equal(TimeSpan.FromMinutes(5), OutboxDispatcher.CalculateBackoff(3).Delay);
        Assert.Equal(TimeSpan.FromMinutes(30), OutboxDispatcher.CalculateBackoff(4).Delay);
        Assert.Equal(TimeSpan.FromHours(2), OutboxDispatcher.CalculateBackoff(5).Delay);
        Assert.True(OutboxDispatcher.CalculateBackoff(6).IsDead);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task LoadSmoke_5000Events_DrainWithNoLoss()
    {
        var repo = new InMemoryOutboxRepository();
        var tenantContext = new AsyncLocalTenantContext();
        const int total = 5000;
        var aggregates = Enumerable.Range(0, 50).Select(_ => Guid.CreateVersion7()).ToArray();

        for (var i = 0; i < total; i++)
        {
            // Monotonic ids within the test so CompareTo matches insertion intent.
            var idBytes = new byte[16];
            BitConverter.GetBytes(i + 1).CopyTo(idBytes, 0);
            idBytes[7] = 0x70; // version 7 nibble region (approx)
            repo.Add(Msg(new Guid(idBytes), aggregates[i % aggregates.Length]));
        }

        var delivered = 0;
        var dispatcher = new OutboxDispatcher(
            repo,
            tenantContext,
            (_, _) =>
            {
                Interlocked.Increment(ref delivered);
                return Task.CompletedTask;
            },
            "w-load");

        var rounds = 0;
        while (rounds++ < total + 10)
        {
            var n = await dispatcher.ProcessBatchAsync(batchSize: 200);
            if (n == 0) break;
        }

        Assert.Equal(total, delivered);
        Assert.Equal(total, repo.GetAllMessages().Count(m => m.DispatchedAt != null));
    }

    [Fact]
    public async Task RetentionCleaner_PurgesOldDispatchedRows()
    {
        var repo = new InMemoryOutboxRepository();
        var old = Msg(Guid.CreateVersion7(), Guid.CreateVersion7());
        old.DispatchedAt = DateTimeOffset.UtcNow.AddDays(-20);
        var recent = Msg(Guid.CreateVersion7(), Guid.CreateVersion7());
        recent.DispatchedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var pending = Msg(Guid.CreateVersion7(), Guid.CreateVersion7());
        repo.AddRange([old, recent, pending]);

        var cleaner = new OutboxRetentionCleaner(repo, TimeSpan.FromDays(14));
        var deleted = await cleaner.RunCleanupAsync();

        Assert.Equal(1, deleted);
        Assert.Equal(2, repo.GetAllMessages().Count);
        Assert.DoesNotContain(repo.GetAllMessages(), m => m.Id == old.Id);
    }
}
