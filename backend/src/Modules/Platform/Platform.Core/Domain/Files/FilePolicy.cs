using System.Globalization;
using Comparator = System.StringComparison;
using Construction.Kernel;

namespace Construction.Platform.Core.Domain.Files;

public enum FileStatus
{
    PendingUpload,
    Uploaded,
    Scanning,
    Available,
    Quarantined,
    Deleted,
    Purged
}

/// <summary>
/// File safety policy from spec 01 §5.4 / BR-PLT-100…106. The declared extension and content type
/// are hints only; a file becomes available only when the magic bytes match and the scanner is clean.
/// </summary>
public static class FilePolicy
{
    public const long DefaultMaxBytes = 25 * 1024 * 1024;
    public const long DrawingMaxBytes = 100 * 1024 * 1024;
    public const string DefaultCategory = "general";
    public const string DrawingCategory = "drawing";

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = "pdf",
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/heic"] = "heic",
        ["image/webp"] = "webp",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",
        ["text/csv"] = "csv",
        ["application/acad"] = "dwg",
        ["image/vnd.dwg"] = "dwg",
        ["application/dxf"] = "dxf",
        ["image/vnd.dxf"] = "dxf",
        ["application/zip"] = "zip"
    };

    public static long MaxBytesFor(string? category) =>
        string.Equals(category, DrawingCategory, Comparator.OrdinalIgnoreCase)
            ? DrawingMaxBytes
            : DefaultMaxBytes;

    public static Result<string> ValidateType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return Result.Failure<string>(Error.Validation(
                "platform.file_type_not_allowed", "A content type is required."));
        }

        return AllowedTypes.ContainsKey(contentType)
            ? Result.Success(contentType)
            : Result.Failure<string>(Error.Validation(
                "platform.file_type_not_allowed", "The file type is not allowed."));
    }

    public static Result<long> ValidateSize(long sizeBytes, string? category)
    {
        if (sizeBytes <= 0)
        {
            return Result.Failure<long>(Error.Validation(
                "platform.file_size_mismatch", "The declared size must be positive."));
        }

        var max = MaxBytesFor(category);
        return sizeBytes > max
            ? Result.Failure<long>(Error.Validation(
                "platform.file_too_large",
                $"The file exceeds the {max / (1024 * 1024)} MB limit."))
            : Result.Success(sizeBytes);
    }

    /// <summary>
    /// Detects the container format from its first bytes. Returns null when the signature is unknown
    /// (text formats such as CSV and DXF carry no magic bytes).
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
        {
            return "application/pdf";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return "image/webp";
        }

        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
        {
            return "application/zip";
        }

        return null;
    }

    /// <summary>
    /// Magic bytes must match the declared type (an executable renamed to .pdf is rejected). ZIP
    /// containers satisfy DOCX/XLSX. Text formats without a signature pass the signature check and
    /// remain subject to the scanner.
    /// </summary>
    public static bool MagicBytesMatch(string declaredContentType, string? detectedContentType)
    {
        if (detectedContentType is null)
        {
            return declaredContentType is "text/csv" or "application/dxf" or "image/vnd.dxf" or "application/acad";
        }

        if (string.Equals(declaredContentType, detectedContentType, Comparator.OrdinalIgnoreCase))
        {
            return true;
        }

        var isZipContainer = string.Equals(detectedContentType, "application/zip", Comparator.OrdinalIgnoreCase);
        var declaredIsZipContainer = declaredContentType is
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/zip";

        return isZipContainer && declaredIsZipContainer;
    }

    /// <summary>Server-generated opaque key; the original filename never appears in the path.</summary>
    public static string StorageKey(Guid tenantId, Guid fileId, DateTimeOffset atUtc) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"t/{tenantId:N}/{atUtc:yyyy}/{atUtc:MM}/{fileId:N}");

    /// <summary>RFC 5987-safe download name: no CR/LF, path separators or control characters.</summary>
    public static string SanitizeDownloadName(string? originalName)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return "file";
        }

        var cleaned = new string(originalName
            .Where(c => !char.IsControl(c) && c != '/' && c != '\\')
            .ToArray())
            .Trim();

        if (cleaned.Length == 0)
        {
            return "file";
        }

        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    public static bool CanTransition(FileStatus from, FileStatus to) =>
        (from, to) switch
        {
            (FileStatus.PendingUpload, FileStatus.Uploaded) => true,
            (FileStatus.Uploaded, FileStatus.Scanning) => true,
            (FileStatus.Scanning, FileStatus.Available) => true,
            (FileStatus.Scanning, FileStatus.Quarantined) => true,
            (FileStatus.Available, FileStatus.Deleted) => true,
            (FileStatus.Deleted, FileStatus.Purged) => true,
            _ => false
        };

    public static bool IsDownloadable(FileStatus status) => status == FileStatus.Available;
}
