namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>Jobs from modules that are switched on in configuration.</summary>
public sealed class JobCatalog
{
    private readonly IReadOnlyList<IJob> _jobs;

    public JobCatalog(IEnumerable<IJob> jobs, ModuleJobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _jobs = jobs.Where(j => options.IsEnabled(j.ModuleCode)).ToList();
    }

    public IReadOnlyList<IJob> Jobs => _jobs;
}
