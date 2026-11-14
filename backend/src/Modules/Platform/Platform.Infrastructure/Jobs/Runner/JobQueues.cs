namespace Construction.Platform.Infrastructure.Jobs.Runner;

public static class JobQueues
{
    public const string Critical = "critical";
    public const string Default = "default";
    public const string Reports = "reports";
    public const string Collectors = "collectors";
    public const string Ai = "ai";

    public static readonly IReadOnlyList<string> All =
    [
        Critical, Default, Reports, Collectors, Ai
    ];

    public static bool IsKnown(string queue) => All.Contains(queue, StringComparer.Ordinal);
}
