namespace Construction.Platform.Core.Application.Files;

public sealed record PresignedUpload(
    string Url,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record PresignedDownload(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredObjectInfo(long SizeBytes, string? ContentType, string? Sha256);

/// <summary>
/// Provider-neutral object storage. Production requires an approved adapter (S3-compatible or
/// equivalent); the domain and API never reference a vendor SDK type.
/// </summary>
public interface IObjectStorage
{
    Task<PresignedUpload> CreateUploadAsync(
        string storageKey,
        string contentType,
        long maxBytes,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<StoredObjectInfo?> HeadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<PresignedDownload> CreateDownloadAsync(
        string storageKey,
        TimeSpan lifetime,
        string safeDownloadName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
}

public enum ScanVerdict
{
    Clean,
    Infected,
    Unavailable
}

public sealed record ScanResult(ScanVerdict Verdict, string? DetectedContentType, string? ThreatName)
{
    public static ScanResult Unavailable { get; } = new(ScanVerdict.Unavailable, null, null);
}

/// <summary>
/// Malware scanner port. An unavailable scanner never reports clean: the file stays in scanning
/// until the 30-minute policy quarantines it with <c>scan_unavailable</c>.
/// </summary>
public interface IFileScanner
{
    Task<ScanResult> ScanAsync(
        Stream content,
        string detectedContentType,
        CancellationToken cancellationToken = default);
}

/// <summary>Record-level read rights for a linked business record (PLT-106 real implementation).</summary>
public interface IFileAccessChecker
{
    Task<bool> CanReadLinkedRecordAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
