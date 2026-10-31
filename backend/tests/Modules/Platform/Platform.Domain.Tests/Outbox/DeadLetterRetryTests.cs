using System.Globalization;
using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Jobs.Outbox;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

/// <summary>
/// PLT-204 retry schedule and dead-letter transition. These tests pin the pure schedule and the
/// dispatcher's attempt bookkeeping; they do NOT prove the PostgreSQL dead-letter write, the
/// conditional replay update or the real advisory/lease behaviour (see D2-PROGRESS.md).
/// </summary>
public sealed class DeadLetterRetryTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            DateTimeOffset.Parse("2026-10-31T09:00:00Z", CultureInfo.InvariantCulture);
    }

    private static OutboxMessage Msg(Guid aggregateId, Guid? id = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        AggregateType = "order",
        AggregateId = aggregateId,
        AggregateVersion = 1,
        Type = "test.order.placed.v1",
        Payload = """{"secret":"never-in-the-dead-row"}""",
        OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        Attempts = 0,
        NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    private static OutboxDispatcher FailingDispatcher(
        InMemoryOutboxRepository repo,
        IClock clock,
        Exception? exception = null)
    {
        exception ??= new InvalidOperationException("handler blew up");
        return new OutboxDispatcher(
            repo,
            new AsyncLocalTenantContext(),
            (_, _) => throw exception,
            workerId: "w-retry",
            clock: clock);
    }

    [Theory]
    [InlineData(1, 10)]                  // +10 s
    [InlineData(2, 60)]                  // +1 m
    [InlineData(3, 300)]                 // +5 m
    [InlineData(4, 1800)]                // +30 m
    [InlineData(5, 7200)]                // +2 h
    public void BackoffSchedule_IsExact(int attempt, int expectedSeconds)
    {
        var (delay, _) = OutboxDispatcher.CalculateBackoff(attempt);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void BackoffSchedule_DeadLetterOnlyAfterTheFifthFailure()
    {
        Assert.False(OutboxDispatcher.CalculateBackoff(1).IsDead);
        Assert.False(OutboxDispatcher.CalculateBackoff(2).IsDead);
        Assert.False(OutboxDispatcher.CalculateBackoff(3).IsDead);
        Assert.False(OutboxDispatcher.CalculateBackoff(4).IsDead);

        // Recipe PLT-204 / Brief PLT-204: "after the 5th failure set dead_at".
        Assert.True(OutboxDispatcher.CalculateBackoff(5).IsDead);
        Assert.True(OutboxDispatcher.CalculateBackoff(6).IsDead);
    }

    [Fact]
    public async Task FirstFourFailures_StayPending_WithExactSchedule()
    {
        var clock = new FixedClock();
        var repo = new InMemoryOutboxRepository();
        var message = Msg(Guid.CreateVersion7());
        repo.Add(message);
        var dispatcher = FailingDispatcher(repo, clock);

        var expected = new[]
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30)
        };

        for (var failure = 1; failure <= 4; failure++)
        {
            await dispatcher.ProcessBatchAsync();

            var stored = repo.GetAllMessages().Single();
            Assert.Equal(failure, stored.Attempts);
            Assert.Equal(clock.UtcNow.Add(expected[failure - 1]), stored.NextAttemptAt);
            Assert.Null(stored.DeadAt);
            Assert.Null(stored.DispatchedAt);
            Assert.Null(stored.LockedBy);

            // The in-memory repository checks eligibility against the wall clock, so make the
            // row due again instead of waiting for the real 10 s / 1 m / 5 m / 30 m delays.
            stored.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        }

        Assert.Empty(repo.GetDeadMessages());
    }

    [Fact]
    public async Task FifthFailure_DeadLettersOnce_WithCategoryAndNoPayload()
    {
        var clock = new FixedClock();
        var repo = new InMemoryOutboxRepository();
        var message = Msg(Guid.CreateVersion7());
        repo.Add(message);
        var dispatcher = FailingDispatcher(repo, clock);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await dispatcher.ProcessBatchAsync();
            var stored = repo.GetAllMessages().Single();
            if (attempt < 5)
            {
                stored.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            }
        }

        var after = repo.GetAllMessages().Single();
        Assert.Equal(5, after.Attempts);
        Assert.NotNull(after.DeadAt);

        var dead = repo.GetDeadMessages().Single();
        Assert.Equal(after.Id, dead.OriginalEventId);
        Assert.Equal(5, dead.Attempts);
        Assert.Equal("application", dead.LastErrorCategory);
        Assert.Equal("InvalidOperationException", dead.ErrorClass);
        Assert.Equal("open", dead.Resolution);
        Assert.Null(dead.ReplayedAt);
        Assert.DoesNotContain("never-in-the-dead-row", dead.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain("handler blew up", dead.LastError, StringComparison.Ordinal);

        // A dead-lettered row is never claimed again, so it cannot be dead-lettered twice.
        var further = await dispatcher.ProcessBatchAsync();
        Assert.Equal(0, further);
        Assert.Single(repo.GetDeadMessages());
    }

    [Fact]
    public async Task RecoveryBeforeExhaustion_DispatchesWithoutDeadRow()
    {
        var clock = new FixedClock();
        var repo = new InMemoryOutboxRepository();
        var message = Msg(Guid.CreateVersion7());
        repo.Add(message);

        var fail = true;
        var delivered = 0;
        var dispatcher = new OutboxDispatcher(
            repo,
            new AsyncLocalTenantContext(),
            (_, _) =>
            {
                if (fail)
                {
                    throw new TimeoutException("upstream timeout");
                }

                delivered++;
                return Task.CompletedTask;
            },
            workerId: "w-recover",
            clock: clock);

        for (var i = 0; i < 3; i++)
        {
            await dispatcher.ProcessBatchAsync();
            repo.GetAllMessages().Single().NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        }

        fail = false;
        await dispatcher.ProcessBatchAsync();

        var stored = repo.GetAllMessages().Single();
        Assert.Equal(3, stored.Attempts);
        Assert.NotNull(stored.DispatchedAt);
        Assert.Null(stored.DeadAt);
        Assert.Equal(1, delivered);
        Assert.Empty(repo.GetDeadMessages());
    }

    [Fact]
    public void ErrorSummary_CategorisesAndNeverEchoesTheExceptionMessage()
    {
        var leaked = JsonSerializer.Serialize(new { secret = "client payload" });

        var (networkCategory, networkClass, networkSummary) =
            DeadLettering.Summarise(new HttpRequestException(leaked));
        var (serializationCategory, serializationClass, _) =
            DeadLettering.Summarise(new JsonException(leaked));
        var (applicationCategory, applicationClass, applicationSummary) =
            DeadLettering.Summarise(new InvalidOperationException(leaked));
        var (timeoutCategory, _, _) = DeadLettering.Summarise(new TimeoutException(leaked));

        Assert.Equal("network", networkCategory);
        Assert.Equal("serialization", serializationCategory);
        Assert.Equal("application", applicationCategory);
        Assert.Equal("network", timeoutCategory);

        // Spec 01 §4.5 error_class: the exception class, never the message.
        Assert.Equal("HttpRequestException", networkClass);
        Assert.Equal("JsonException", serializationClass);
        Assert.Equal("InvalidOperationException", applicationClass);

        foreach (var summary in new[] { networkSummary, applicationSummary })
        {
            Assert.DoesNotContain("client payload", summary, StringComparison.Ordinal);
            Assert.True(summary.Length <= 500, "The stored error summary must stay bounded.");
        }
    }
}
