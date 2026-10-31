using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

public interface IJobRunStore
{
    Task InsertAsync(JobRun run, CancellationToken cancellationToken = default);
    Task UpdateAsync(JobRun run, CancellationToken cancellationToken = default);
    Task<JobRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    IReadOnlyList<JobRun> ListAll();
}
