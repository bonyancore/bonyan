namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Config section Modules:{Code}:Enabled — only switched-on modules register jobs/consumers.
/// </summary>
public sealed class ModuleJobOptions
{
    public const string SectionName = "Modules";

    public bool Platform { get; set; } = true;
    public bool Cost { get; set; }
    public bool Cash { get; set; }

    public bool IsEnabled(string moduleCode) => moduleCode.ToLowerInvariant() switch
    {
        "platform" => Platform,
        "cost" => Cost,
        "cash" => Cash,
        _ => false
    };
}
