using Construction.Platform.Core.Application.Files;

namespace Construction.Platform.Infrastructure.Files;

/// <summary>
/// Fail-closed object storage used until an approved provider is configured. It never pretends an
/// upload exists: completion fails with <c>upload_incomplete</c> and no download URL is issued.
/// </summary>
public sealed class UnavailableObjectStorage : IObjectStorage
{
    public Task<PresignedUpload> CreateUploadAsync(
        string storageKey,
        string contentType,
        long maxBytes,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Object storage is not configured. Configure an approved storage adapter before accepting uploads.");

    public Task<StoredObjectInfo?> HeadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredObjectInfo?>(null);

    public Task<PresignedDownload> CreateDownloadAsync(
        string storageKey,
        TimeSpan lifetime,
        string safeDownloadName,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Object storage is not configured.");

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Object storage is not configured.");
}

/// <summary>
/// Fail-closed scanner. It always reports <see cref="ScanVerdict.Unavailable"/>; the scan policy then
/// quarantines the file instead of marking it clean.
/// </summary>
public sealed class UnavailableFileScanner : IFileScanner
{
    public Task<ScanResult> ScanAsync(
        Stream content,
        string detectedContentType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ScanResult.Unavailable);
}
