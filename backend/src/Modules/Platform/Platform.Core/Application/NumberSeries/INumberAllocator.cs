using System.Data.Common;

namespace Construction.Platform.Core.Application.NumberSeries;

/// <summary>
/// Request for one document number. The owning module passes its own project code; the Platform
/// module never reads another module's tables.
/// </summary>
public sealed record NumberRequest(
    string SeriesCode,
    Guid? ProjectId = null,
    string? ProjectCode = null,
    DateOnly? DocumentDate = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? TenantTimeZoneId = null);

public sealed record AllocatedNumber(string Number, long Sequence, string SeriesCode, string ScopeKey);

/// <summary>A preview never consumes a number (UC-206-05).</summary>
public sealed record NumberPreview(string SeriesCode, string NextNumber, string ScopeKey);

/// <summary>
/// Gapless number allocation (PLT-206). The implementation MUST run on the caller's connection and
/// transaction: a rollback of the caller releases the number, and the tenant GUC is the caller's.
/// Callers allocating several series must allocate them in ascending <c>SeriesCode</c> order to
/// avoid deadlocks.
/// </summary>
public interface INumberAllocator
{
    Task<AllocatedNumber> AllocateAsync(
        DbConnection connection,
        DbTransaction transaction,
        NumberRequest request,
        CancellationToken cancellationToken = default);

    Task<NumberPreview> PreviewAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string seriesCode,
        Guid? projectId,
        string? projectCode,
        DateOnly? documentDate,
        string? tenantTimeZoneId,
        CancellationToken cancellationToken = default);
}
