using Xunit;
using System.Text;
using Construction.Web.Idempotency;

namespace Construction.Web.Tests.Idempotency;

public sealed class IdempotencyDecisionEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-11-21T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly TimeSpan Takeover = TimeSpan.FromSeconds(60);

    [Fact]
    public void No_row_proceeds()
    {
        Assert.Equal(
            IdempotencyDecisionKind.Proceed,
            Decide(null, Hash("a")).Kind);
    }

    [Fact]
    public void Completed_same_hash_replays()
    {
        var record = Record(IdempotencyRecord.Completed, Hash("a"), Now.AddMinutes(-5));

        var decision = Decide(record, Hash("a"));

        Assert.Equal(IdempotencyDecisionKind.Replay, decision.Kind);
        Assert.Same(record, decision.Record);
    }

    [Fact]
    public void Completed_too_large_reports_too_large()
    {
        var record = Record(IdempotencyRecord.Completed, Hash("a"), Now.AddMinutes(-5)) with
        {
            ReplayTooLarge = true
        };

        Assert.Equal(IdempotencyDecisionKind.ReplayTooLarge, Decide(record, Hash("a")).Kind);
    }

    [Fact]
    public void Completed_different_hash_is_reuse()
    {
        var record = Record(IdempotencyRecord.Completed, Hash("a"), Now.AddMinutes(-5));

        Assert.Equal(IdempotencyDecisionKind.KeyReuse, Decide(record, Hash("b")).Kind);
    }

    [Fact]
    public void Fresh_in_progress_same_hash_conflicts()
    {
        var record = Record(IdempotencyRecord.InProgress, Hash("a"), Now.AddSeconds(-10));

        Assert.Equal(IdempotencyDecisionKind.ConflictInProgress, Decide(record, Hash("a")).Kind);
    }

    [Fact]
    public void Stale_in_progress_same_hash_allows_takeover()
    {
        var record = Record(IdempotencyRecord.InProgress, Hash("a"), Now.AddSeconds(-61));

        Assert.Equal(IdempotencyDecisionKind.Proceed, Decide(record, Hash("a")).Kind);
    }

    [Fact]
    public void In_progress_different_hash_is_reuse()
    {
        var record = Record(IdempotencyRecord.InProgress, Hash("a"), Now.AddSeconds(-10));

        Assert.Equal(IdempotencyDecisionKind.KeyReuse, Decide(record, Hash("b")).Kind);
    }

    private static IdempotencyDecision Decide(IdempotencyRecord? record, byte[] hash) =>
        IdempotencyDecisionEngine.Decide(record, hash, Now, Takeover);

    private static IdempotencyRecord Record(string status, byte[] hash, DateTimeOffset createdAt) =>
        new(
            status,
            hash,
            201,
            "application/json",
            [1, 2, 3],
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            createdAt,
            Now.AddHours(24));

    private static byte[] Hash(string value) => Encoding.UTF8.GetBytes(value);
}
