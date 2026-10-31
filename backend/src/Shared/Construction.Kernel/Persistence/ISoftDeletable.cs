namespace Construction.Kernel.Persistence;

/// <summary>
/// Reference data entities supporting logical soft deletion.
/// Entities implementing this interface are expected to be logically deleted by setting IsDeleted = true.
/// Hard deletion via DbContext.Remove() is not prevented at the infrastructure layer but remains a
/// domain-level decision subject to tenant guards.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}

