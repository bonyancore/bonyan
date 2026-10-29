namespace Construction.Kernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock(TimeProvider time) : IClock
{
    public SystemClock() : this(TimeProvider.System) { }
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
