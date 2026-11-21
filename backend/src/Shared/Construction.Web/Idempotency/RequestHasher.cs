using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Construction.Web.Idempotency;

/// <summary>
/// Computes the idempotency request hash: SHA-256 over method + route path + canonical body.
/// Canonical JSON sorts object properties ordinally, keeps array order, normalises numbers
/// (1.0 == 1) and removes insignificant whitespace; non-JSON bodies are hashed raw.
/// </summary>
public static class RequestHasher
{
    public static byte[] Compute(string method, string path, byte[] body, string? contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(path);
        body ??= [];

        var canonicalBody = IsJson(contentType) ? TryCanonicalizeJson(body) : Encoding.UTF8.GetString(body);
        var prefix = Encoding.UTF8.GetBytes($"{method.ToUpperInvariant()}\n{path}\n");
        var bodyBytes = Encoding.UTF8.GetBytes(canonicalBody);

        var buffer = new byte[prefix.Length + bodyBytes.Length];
        prefix.CopyTo(buffer, 0);
        bodyBytes.CopyTo(buffer, prefix.Length);

        return SHA256.HashData(buffer);
    }

    /// <summary>Constant-time comparison of two hashes (R-205-03).</summary>
    public static bool HashesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

    public static string ToKeyFingerprint(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();

    private static bool IsJson(string? contentType) =>
        contentType is not null &&
        (contentType.Contains("json", StringComparison.OrdinalIgnoreCase));

    private static string TryCanonicalizeJson(byte[] body)
    {
        if (body.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var builder = new StringBuilder(body.Length);
            WriteElement(document.RootElement, builder);
            return builder.ToString();
        }
        catch (JsonException)
        {
            return Encoding.UTF8.GetString(body);
        }
    }

    private static void WriteElement(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    builder.Append(JsonSerializer.Serialize(property.Name));
                    builder.Append(':');
                    WriteElement(property.Value, builder);
                }

                builder.Append('}');
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteElement(item, builder);
                }

                builder.Append(']');
                break;

            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(element.GetString()));
                break;

            case JsonValueKind.Number:
                var raw = element.GetRawText();
                builder.Append(decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    ? number.ToString("0.############################", CultureInfo.InvariantCulture)
                    : raw);
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            default:
                builder.Append("null");
                break;
        }
    }
}
