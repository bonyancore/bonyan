using Construction.Kernel;
using Construction.Platform.Core.Application.Outbox;
using Construction.Platform.Domain.Tests.Jobs;
using Construction.Platform.Infrastructure.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Npgsql;
using Xunit;

namespace Construction.Platform.Domain.Tests.Outbox;

public sealed class DeadLetterPostgresTests
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=postgres;Password=postgres_dev";

    private const string AppConnectionString =
        "Host=localhost;Port=5432;Database=construction;Username=app;Password=app_dev_pw";

    private sealed class FakeConfig(string defaultConnection) : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "ConnectionStrings:Default" ? defaultConnection : null;
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string key) => new FakeSection(key, defaultConnection);
    }

    private sealed class FakeSection(string key, string defaultConnection) : IConfigurationSection
    {
        public string Key => key;
        public string Path => key;
        public string? Value { get => null; set { } }
        public string? this[string subKey]
        {
            get => key == "ConnectionStrings" && subKey == "Default" ? defaultConnection : null;
            set { }
        }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string subKey) => new FakeSection(subKey, defaultConnection);
    }

    private static DeadLetterService CreateService()
    {
        return new DeadLetterService(new FakeConfig(AppConnectionString), new SystemClock());
    }

    private static async Task SeedDeadLetterAsync(
        Guid id,
        Guid tenantId,
        Guid originalEventId,
        string resolution = "open")
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO platform.outbox_dead
                (id, original_event_id, tenant_id, type, payload, aggregate_type, aggregate_id,
                 aggregate_version, attempts, last_error, last_error_category, error_class,
                 dead_at, resolution)
            VALUES
                (@id, @origId, @tId, 'test.event.dead.v1', '{"data":"test"}'::jsonb, 'test_agg', @id,
                 1, 5, 'Network timeout', 'network', 'TransientException',
                 now(), @res)
            ON CONFLICT (id) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("origId", originalEventId);
        cmd.Parameters.AddWithValue("tId", tenantId);
        cmd.Parameters.AddWithValue("res", resolution);

        await cmd.ExecuteNonQueryAsync();
    }

    [LivePgFact]
    public async Task DeadLetterReplay_AtomicTransaction_RequeuesAndMarksReplayed()
    {
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var deadId = Guid.NewGuid();
        var origEventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await SeedDeadLetterAsync(deadId, tenantId, origEventId, "open");

        // 1. Replay (tenantId, actorId, deadMessageId, reason)
        var result = await service.ReplayAsync(tenantId, actorId, deadId, "Operational retry");
        Assert.Equal(DeadLetterResolutionState.Resolved, result.State);

        // 2. Verify dead letter marked as replayed
        var dead = await service.GetAsync(tenantId, deadId);
        Assert.NotNull(dead);
        Assert.Equal("replayed", dead.Resolution);
        Assert.Equal("Operational retry", dead.ResolutionNote);
        Assert.NotNull(dead.ReplayedAt);

        // 3. Verify new outbox message inserted
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM platform.outbox WHERE tenant_id = @tId AND type = 'test.event.dead.v1';";
        checkCmd.Parameters.AddWithValue("tId", tenantId);
        var outboxCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(outboxCount >= 1);
    }

    [LivePgFact]
    public async Task DeadLetterReplay_SecondCallReturnsAlreadyResolved()
    {
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var deadId = Guid.NewGuid();
        var origEventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await SeedDeadLetterAsync(deadId, tenantId, origEventId, "open");

        // First replay: success
        var first = await service.ReplayAsync(tenantId, actorId, deadId, "First click");
        Assert.Equal(DeadLetterResolutionState.Resolved, first.State);

        // Second concurrent/duplicate replay: AlreadyResolved
        var second = await service.ReplayAsync(tenantId, actorId, deadId, "Second click");
        Assert.Equal(DeadLetterResolutionState.AlreadyResolved, second.State);
    }

    [LivePgFact]
    public async Task DeadLetterDiscard_RequiresNote_AndSetsResolution()
    {
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var deadId = Guid.NewGuid();
        var origEventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await SeedDeadLetterAsync(deadId, tenantId, origEventId, "open");

        // Discard with empty note throws ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DiscardAsync(tenantId, actorId, deadId, "   "));

        // Discard with valid note succeeds
        var result = await service.DiscardAsync(tenantId, actorId, deadId, "Not needed anymore");
        Assert.Equal(DeadLetterResolutionState.Resolved, result.State);

        var dead = await service.GetAsync(tenantId, deadId);
        Assert.NotNull(dead);
        Assert.Equal("discarded", dead.Resolution);
        Assert.Equal("Not needed anymore", dead.ResolutionNote);
    }

    [LivePgFact]
    public async Task DeadLetterTenantIsolation_CannotAccessOtherTenantDeadLetter()
    {
        var service = CreateService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var deadId = Guid.NewGuid();
        var origEventId = Guid.NewGuid();

        await SeedDeadLetterAsync(deadId, tenantA, origEventId, "open");

        // Tenant B cannot retrieve Tenant A's dead letter
        var getByB = await service.GetAsync(tenantB, deadId);
        Assert.Null(getByB);

        // Tenant B cannot replay Tenant A's dead letter
        var replayByB = await service.ReplayAsync(tenantB, Guid.NewGuid(), deadId, "Unauthorized replay");
        Assert.Equal(DeadLetterResolutionState.NotFound, replayByB.State);
    }
}
