using Construction.Kernel.Pagination;

namespace Construction.Web.Pagination;

/// <summary>
/// Extension helpers for keyset pagination parameters.
/// The actual EF Core <c>IQueryable&lt;T&gt;</c> extension (<c>ToKeysetPageAsync</c>)
/// lives in <c>Construction.Persistence</c> to keep this project free of EF Core dependencies.
/// </summary>
public static class KeysetQueryExtensions
{
    private const int MaxPageSize = 200;

    /// <summary>
    /// Clamps a caller-supplied page size to the allowed range [1, 200].
    /// </summary>
    public static int ClampPageSize(int requested) => Math.Clamp(requested, 1, MaxPageSize);
}
