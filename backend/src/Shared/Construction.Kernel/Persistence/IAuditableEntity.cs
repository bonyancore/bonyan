namespace Construction.Kernel.Persistence;

/// <summary>
/// Standard audit columns required on all persistent entities unless explicitly exempt.
/// Managed automatically by AuditSaveChangesInterceptor.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

