using System.Data.Common;
using Construction.Kernel;
using Construction.Platform.Contracts;

namespace Construction.Platform.Core.Application.NumberSeries;

public sealed record NumberAllocationPage(
    IReadOnlyList<NumberAllocationDto> Items,
    bool HasMore,
    DateTimeOffset? NextCursorAllocatedAt,
    Guid? NextCursorId);

public interface INumberSeriesStore
{
    Task<IReadOnlyList<NumberSeriesDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<NumberSeriesDto?> GetAsync(string code, CancellationToken cancellationToken = default);

    Task<Result<NumberSeriesDto>> UpdateAsync(
        string code,
        UpdateNumberSeriesRequest request,
        int expectedRowVersion,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<NumberAllocationPage> ListAllocationsAsync(
        string code,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset? cursorAllocatedAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> EnsureDefaultsAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
