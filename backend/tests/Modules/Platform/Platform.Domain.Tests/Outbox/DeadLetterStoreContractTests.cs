using System.Globalization;
using Construction.Kernel;
using Construction.Platform.Core.Application.Outbox;
using Construction.Platform.Infrastructure.Application;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

/// <summary>
/// Pins the dead-letter resolution contract the admin endpoints depend on: the conditional
/// <c>resolution = 'open'</c> transition (a second replay is 409), the mandatory discard note,
/// tenant isolation and keyset ordering.
/// </summary>
/// <remarks>
/// These run against <see cref="InMemoryDeadLetterStore"/>, so they do NOT prove the PostgreSQL
/// statements in <c>DeadLetterService</c> or the endpoint wiring. The same cases must be re-run
/// against a real database once D2_TEST_PG is available (see D2-PROGRESS.md, "Replace later").
/// </remarks>
public sealed class DeadLetterStoreContractTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-31T09:00:00Z", CultureInfo.InvariantCulture);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
    }

    private static (InMemoryDeadLetterStore Store, Guid Tenant, Guid Actor) CreateSut()
    {
        var store = new InMemoryDeadLetterStore(new FixedClock());
        return (store, Guid.CreateVersion7(), Guid.CreateVersion7());
    }

    [Fact]
    public async Task Replay_OpenRow_ResolvesOnceWithWhoWhenWhy()
    {
        var (store, tenant, actor) = CreateSut();
        var dead = store.Seed(tenant);

        var result = await store.ReplayAsync(tenant, actor, dead.Id, "consumer fixed in v1.4");

        Assert.Equal(DeadLetterResolutionState.Resolved, result.State);
        Assert.Equal(Now, result.At);

        var row = await store.GetAsync(tenant, dead.Id);
        Assert.NotNull(row);
        Assert.Equal("replayed", row!.Resolution);
        Assert.Equal(actor, row.ResolvedBy);
        Assert.Equal(Now, row.ResolvedAt);
        Assert.Equal("consumer fixed in v1.4", row.ResolutionNote);
        Assert.Equal(Now, row.ReplayedAt);
    }

    [Fact]
    public async Task Replay_SecondTime_IsAlreadyResolved()
    {
        var (store, tenant, actor) = CreateSut();
        var dead = store.Seed(tenant);

        await store.ReplayAsync(tenant, actor, dead.Id, "first");

        // The endpoint maps this to 409 `dead_letter_resolved`.
        var second = await store.ReplayAsync(tenant, actor, dead.Id, "second");

        Assert.Equal(DeadLetterResolutionState.AlreadyResolved, second.State);
    }

    [Fact]
    public async Task Replay_UnknownId_IsNotFound()
    {
        var (store, tenant, actor) = CreateSut();

        var result = await store.ReplayAsync(tenant, actor, Guid.CreateVersion7(), "why not");

        Assert.Equal(DeadLetterResolutionState.NotFound, result.State);
    }

    [Fact]
    public async Task Replay_OtherTenantRow_IsNotFound()
    {
        var (store, tenant, actor) = CreateSut();
        var dead = store.Seed(tenant);

        var result = await store.ReplayAsync(Guid.CreateVersion7(), actor, dead.Id, "cross tenant attempt");

        Assert.Equal(DeadLetterResolutionState.NotFound, result.State);
        var row = await store.GetAsync(tenant, dead.Id);
        Assert.Equal("open", row!.Resolution);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Discard_WithoutNote_IsRejected(string note)
    {
        var (store, tenant, actor) = CreateSut();
        var dead = store.Seed(tenant);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.DiscardAsync(tenant, actor, dead.Id, note));

        var row = await store.GetAsync(tenant, dead.Id);
        Assert.Equal("open", row!.Resolution);
    }

    [Fact]
    public async Task Discard_WithNote_ResolvesAndBlocksLaterReplay()
    {
        var (store, tenant, actor) = CreateSut();
        var dead = store.Seed(tenant);

        var discard = await store.DiscardAsync(tenant, actor, dead.Id, "event no longer needed");
        Assert.Equal(DeadLetterResolutionState.Resolved, discard.State);

        var row = await store.GetAsync(tenant, dead.Id);
        Assert.Equal("discarded", row!.Resolution);
        Assert.Equal("event no longer needed", row.ResolutionNote);
        Assert.Null(row.ReplayedAt);

        var replay = await store.ReplayAsync(tenant, actor, dead.Id, "too late");
        Assert.Equal(DeadLetterResolutionState.AlreadyResolved, replay.State);
    }

    [Fact]
    public async Task List_FiltersByResolutionAndEventType_NewestFirst()
    {
        var (store, tenant, _) = CreateSut();
        var older = store.Seed(tenant, deadAt: Now.AddMinutes(-10));
        var newer = store.Seed(tenant, deadAt: Now.AddMinutes(-1));
        var other = store.Seed(tenant, type: "other.event.v1"); // deadAt defaults to Now
        await store.DiscardAsync(tenant, Guid.CreateVersion7(), older.Id, "handled manually");

        // Newest first, and the discarded row is no longer 'open'.
        var open = await store.ListAsync(tenant, null, "open", null, null, 50);
        Assert.Equal([other.Id, newer.Id], open.Select(r => r.Id));

        var byType = await store.ListAsync(tenant, "other.event.v1", null, null, null, 50);
        Assert.Equal([other.Id], byType.Select(r => r.Id));

        var all = await store.ListAsync(tenant, null, null, null, null, 50);
        Assert.Equal([other.Id, newer.Id, older.Id], all.Select(r => r.Id));
    }

    [Fact]
    public async Task List_KeysetCursor_ReturnsTheNextPage()
    {
        var (store, tenant, _) = CreateSut();
        var first = store.Seed(tenant, deadAt: Now.AddMinutes(-3));
        var second = store.Seed(tenant, deadAt: Now.AddMinutes(-2));
        var third = store.Seed(tenant, deadAt: Now.AddMinutes(-1));

        var page1 = await store.ListAsync(tenant, null, null, null, null, 2);
        Assert.Equal([third.Id, second.Id], page1.Take(2).Select(r => r.Id));
        // The store returns pageSize + 1 rows so the caller can compute hasMore.
        Assert.Equal(3, page1.Count);

        var page2 = await store.ListAsync(tenant, null, null, page1[1].DeadAt, page1[1].Id, 2);
        Assert.Equal([first.Id], page2.Select(r => r.Id));
    }

    [Fact]
    public async Task List_OtherTenantRows_AreInvisible()
    {
        var (store, tenant, _) = CreateSut();
        store.Seed(Guid.CreateVersion7());

        var rows = await store.ListAsync(tenant, null, null, null, null, 50);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Lag_ReportsSeededCounts()
    {
        var (store, tenant, _) = CreateSut();
        store.SeedLag(42.5, 3);

        var lag = await store.GetLagAsync(tenant);

        Assert.Equal(42.5, lag.LagSeconds);
        Assert.Equal(3, lag.PendingCount);
    }
}
