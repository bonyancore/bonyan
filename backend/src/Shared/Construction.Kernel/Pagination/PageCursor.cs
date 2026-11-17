using System.Buffers.Binary;

namespace Construction.Kernel.Pagination;

/// <summary>
/// Opaque, Base64url-encoded keyset pagination cursor carrying <c>(createdAt, id)</c>.
/// Matches the keyset index <c>(tenant_id, created_at DESC, id DESC)</c>.
/// Strictly enforces UTC DateTimeKind and truncates sub-microsecond ticks to match PostgreSQL timestamptz.
/// </summary>
public sealed record PageCursor
{
    private const int ByteLength = 24; // 8 bytes ticks + 16 bytes Guid

    public DateTime CreatedAt { get; init; }
    public Guid Id { get; init; }

    public PageCursor(DateTime createdAt, Guid id)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"Cursor CreatedAt must be in UTC (DateTimeKind.Utc). Received: {createdAt.Kind}.",
                nameof(createdAt));
        }

        // Truncate sub-microsecond ticks (1 microsecond = 10 ticks)
        var ticks = createdAt.Ticks;
        CreatedAt = new DateTime(ticks - (ticks % 10), DateTimeKind.Utc);
        Id = id;
    }

    /// <summary>Encodes this cursor to a URL-safe Base64url string.</summary>
    public string Encode()
    {
        var bytes = new byte[ByteLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, CreatedAt.Ticks);
        Id.TryWriteBytes(bytes.AsSpan(8));
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Attempts to decode a Base64url cursor string.
    /// Returns <c>false</c> if the input is null, empty, or malformed.
    /// </summary>
    public static bool TryDecode(string? encoded, out PageCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            var bytes = Base64UrlDecode(encoded);
            if (bytes.Length != ByteLength)
            {
                return false;
            }

            var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
            var id = new Guid(bytes.AsSpan(8, 16));
            cursor = new PageCursor(new DateTime(ticks, DateTimeKind.Utc), id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return Convert.FromBase64String(padded);
    }
}
