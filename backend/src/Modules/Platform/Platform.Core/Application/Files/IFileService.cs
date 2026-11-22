using Construction.Kernel;
using Construction.Platform.Contracts;

namespace Construction.Platform.Core.Application.Files;

public interface IFileService
{
    Task<Result<CreateUploadResponse>> CreateUploadAsync(
        CreateUploadRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<CompleteUploadResponse>> CompleteUploadAsync(
        Guid fileId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<FileMetadataDto?> GetAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<Result<FileDownloadResponse>> DownloadAsync(
        Guid fileId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<FileMetadataDto>> DeleteAsync(
        Guid fileId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<FileLinkDto>> AddLinkAsync(
        Guid fileId,
        CreateFileLinkRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileLinkDto>> ListLinksAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the scan state machine. An unavailable scanner leaves the file in scanning until the
    /// 30-minute policy expires, then quarantines it with <c>scan_unavailable</c>.
    /// </summary>
    Task<Result<FileMetadataDto>> ScanAsync(Guid fileId, CancellationToken cancellationToken = default);
}
