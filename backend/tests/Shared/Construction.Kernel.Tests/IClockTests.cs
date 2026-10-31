using Xunit;

namespace Construction.Kernel.Tests;

public class IClockTests
{
    [Fact]
    public void SystemClock_DefaultConstructor_UsesSystemTime()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var clockTime = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(clockTime, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void SystemClock_WithCustomTimeProvider_ReturnsProviderTime()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        var fakeProvider = new FakeTimeProvider(fixedTime);

        var clock = new SystemClock(fakeProvider);

        Assert.Equal(fixedTime, clock.UtcNow);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

