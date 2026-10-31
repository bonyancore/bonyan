namespace Construction.Kernel.Persistence;

/// <summary>
/// Optimistic concurrency token mapped to PostgreSQL's system xmin column.
/// Automatically configured via IsRowVersion() in ModuleDbContext.
/// </summary>
public interface IConcurrencyVersioned
{
    uint RowVersion { get; set; }
}

