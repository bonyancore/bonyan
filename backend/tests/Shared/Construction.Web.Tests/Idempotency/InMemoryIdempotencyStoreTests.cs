using Xunit;
using Construction.Web.Idempotency;

namespace Construction.Web.Tests.Idempotency;

public sealed class InMemoryIdempotencyStoreTests
{
    private static readonly byte[] Hash = [1, 2, 3];

    [Fact]
    public async Task Second_concurrent_claim_loses()
    {
        var store = new InMemoryIdempotencyStore();
        var scope = Scope();

        Assert.True(await store.TryClaimAsync(scope, Hash, TimeSpan.FromHours(24)));
        Assert.False(await store.TryClaimAsync(scope, Hash, TimeSpan.FromHours(24)));
    }

    [Fact]
    public async Task Completion_is_replayable_and_release_removes_only_in_progress()
    {
        var store = new InMemoryIdempotencyStore();
        var scope = Scope();

        Assert.True(await store.TryClaimAsync(scope, Hash, TimeSpan.FromHours(24)));
        await store.CompleteAsync(
            scope, 201, "application/json", [9], new Dictionary<string, string>(), false);

        var record = await store.FindAsync(scope);
        Assert.NotNull(record);
        Assert.True(record!.IsCompleted);
        Assert.Equal(201, record.ResponseStatus);

        await store.ReleaseAsync(scope);
        Assert.NotNull(await store.FindAsync(scope));
    }

    [Fact]
    public async Task Expired_key_is_claimed_again()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryIdempotencyStore(clock);
        var scope = Scope();

        Assert.True(await store.TryClaimAsync(scope, Hash, TimeSpan.FromSeconds(1)));
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(await store.TryClaimAsync(scope, Hash, TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task Stale_in_progress_claim_can_be_taken_over_only_after_the_threshold()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryIdempotencyStore(clock);
        var scope = Scope();

        Assert.True(await store.TryClaimAsync(scope, Hash, TimeSpan.FromHours(24)));
        Assert.False(await store.TryTakeOverAsync(scope, TimeSpan.FromSeconds(60), TimeSpan.FromHours(24)));

        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.True(await store.TryTakeOverAsync(scope, TimeSpan.FromSeconds(60), TimeSpan.FromHours(24)));
    }

    [Fact]
    public async Task Purge_removes_only_expired_rows()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryIdempotencyStore(clock);
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        IdempotencyScope Scoped(string key) => new(tenant, user, key, "POST", "/x");

        await store.TryClaimAsync(Scoped("expired-key"), Hash, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(2));
        await store.TryClaimAsync(Scoped("live-key-1"), Hash, TimeSpan.FromHours(1));

        var removed = await store.PurgeExpiredAsync(10);

        Assert.Equal(1, removed);
        Assert.Null(await store.FindAsync(Scoped("expired-key")));
        Assert.NotNull(await store.FindAsync(Scoped("live-key-1")));
    }

    [Fact]
    public async Task Different_principals_and_tenants_do_not_collide()
    {
        var store = new InMemoryIdempotencyStore();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var user = Guid.NewGuid();

        Assert.True(await store.TryClaimAsync(
            new IdempotencyScope(tenantA, user, "same-key-1", "POST", "/x"), Hash, TimeSpan.FromHours(1)));
        Assert.True(await store.TryClaimAsync(
            new IdempotencyScope(tenantB, user, "same-key-1", "POST", "/x"), Hash, TimeSpan.FromHours(1)));
        Assert.True(await store.TryClaimAsync(
            new IdempotencyScope(tenantA, Guid.NewGuid(), "same-key-1", "POST", "/x"), Hash, TimeSpan.FromHours(1)));
    }

    private static IdempotencyScope Scope(string key = "key-123456") =>
        new(Guid.NewGuid(), Guid.NewGuid(), key, "POST", "/api/v1/x");

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
