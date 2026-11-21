using Xunit;
using Construction.Persistence;
using Construction.Platform.Infrastructure.Jobs.Outbox;

namespace Construction.Platform.Domain.Tests.Outbox;

/// <summary>
/// T-204-I08 guard for R-204-01. The dispatcher still keeps one retry/dead state per outbox row
/// (single-consumer shortcut accepted for R1). This test pins that contract: when per-consumer
/// deliveries are introduced, the repository surface changes and this guard fails, forcing the
/// dead-letter schema and API to gain a <c>consumer</c> dimension at the same time.
/// </summary>
public sealed class DeadLetterSingleConsumerGuardTests
{
    [Fact]
    public void Outbox_repository_exposes_one_retry_state_per_event()
    {
        var markFailed = typeof(IOutboxRepository).GetMethod(nameof(IOutboxRepository.MarkFailedAsync));

        Assert.NotNull(markFailed);

        var parameters = markFailed!.GetParameters();
        Assert.DoesNotContain(parameters, p => p.Name == "consumer");
        Assert.Contains(parameters, p => p.Name == "attempts");
        Assert.Contains(parameters, p => p.Name == "isDead");
    }

    [Fact]
    public void Dead_letter_row_has_no_consumer_dimension_yet()
    {
        var properties = typeof(OutboxMessage).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("Consumer", properties);
    }
}
