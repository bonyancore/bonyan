namespace Construction.Platform.Contracts;

public sealed record CreateUploadRequest(
    string ContentType,
    long SizeBytes,
    string? Category,
    string? OriginalName,
    string? Sha256,
    string? ClientRef,
    Guid? ProjectId,
    string? EntityType,
    Guid? EntityId,
    string? Visibility);

public sealed record CreateUploadResponse(
    Guid FileId,
    Guid UploadSessionId,
    string UploadUrl,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record FileMetadataDto(
    Guid Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    string? Sha256,
    string Status,
    string ScanStatus,
    string? Category,
    string Visibility,
    Guid? ProjectId,
    bool LegalHold,
    int RowVersion,
    DateTimeOffset CreatedAt)
{
    public bool IsDownloadable => string.Equals(Status, "available", StringComparison.Ordinal);
}

public sealed record CompleteUploadResponse(Guid FileId, string Status, string ScanStatus);

public sealed record FileDownloadResponse(string Url, DateTimeOffset ExpiresAt, string FileName);

public sealed record CreateFileLinkRequest(
    string EntityType,
    Guid EntityId,
    string? LinkRole,
    string? Caption);

public sealed record FileLinkDto(
    Guid Id,
    Guid FileId,
    string EntityType,
    Guid EntityId,
    string? LinkRole,
    string? Caption,
    DateTimeOffset CreatedAt);
