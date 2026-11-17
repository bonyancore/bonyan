using System.Text.RegularExpressions;

namespace Construction.Web.ETag;

/// <summary>
/// Parsed representation of an HTTP If-Match header per RFC 7232.
/// </summary>
public sealed record IfMatchCondition
{
    public bool IsMissing { get; init; }
    public bool IsMalformed { get; init; }
    public string? MalformedReason { get; init; }
    public bool IsWildcard { get; init; }
    public IReadOnlyList<string> EntityTags { get; init; } = [];
    public IReadOnlyList<string> WeakEntityTags { get; init; } = [];

    public static IfMatchCondition Missing() => new() { IsMissing = true };

    public static IfMatchCondition Malformed(string reason) => new()
    {
        IsMalformed = true,
        MalformedReason = reason
    };

    public static IfMatchCondition Wildcard() => new() { IsWildcard = true };

    public static IfMatchCondition FromTags(IReadOnlyList<string> strongTags, IReadOnlyList<string> weakTags) => new()
    {
        EntityTags = strongTags,
        WeakEntityTags = weakTags
    };

    /// <summary>
    /// Matches against an entity's current strong ETag (e.g. "\"42\"").
    /// Weak tags never satisfy strong comparison for state mutation per RFC 7232 §2.3.2.
    /// </summary>
    public bool Matches(string currentStrongEtag)
    {
        if (IsMissing || IsMalformed)
        {
            return false;
        }

        if (IsWildcard)
        {
            return true;
        }

        foreach (var tag in EntityTags)
        {
            if (string.Equals(tag, currentStrongEtag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Converts row versions to and from RFC 7232 entity-tags, and parses If-Match headers.
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Formats a numeric row version as an RFC 7232 strong ETag (e.g. "\"42\"").
    /// </summary>
    public static string ToETag(uint rowVersion) => $"\"{rowVersion}\"";

    /// <summary>
    /// Attempts to parse a strong ETag string back to a uint row version.
    /// </summary>
    public static bool TryParseETag(string? etagHeaderValue, out uint rowVersion)
    {
        rowVersion = 0;
        if (string.IsNullOrWhiteSpace(etagHeaderValue))
        {
            return false;
        }

        var trimmed = etagHeaderValue.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            trimmed = trimmed[1..^1];
        }

        return uint.TryParse(trimmed, out rowVersion);
    }

    /// <summary>
    /// Parses an If-Match header value according to RFC 7232 entity-tag grammar.
    /// </summary>
    public static IfMatchCondition ParseIfMatch(IEnumerable<string>? headerValues)
    {
        if (headerValues == null)
        {
            return IfMatchCondition.Missing();
        }

        var rawList = headerValues.ToList();
        if (rawList.Count == 0)
        {
            return IfMatchCondition.Missing();
        }

        var strongTags = new List<string>();
        var weakTags = new List<string>();
        var sawWildcard = false;
        var sawAnyToken = false;

        foreach (var rawValue in rawList)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return IfMatchCondition.Malformed("If-Match header value cannot be empty or whitespace-only.");
            }

            if (sawWildcard)
            {
                return IfMatchCondition.Malformed("Wildcard '*' cannot be repeated or combined with other entity-tags or list separators.");
            }

            var index = 0;
            var span = rawValue.AsSpan();

            while (index < span.Length)
            {
                // Skip leading OWS (SP / HTAB) and permitted empty list element commas (RFC 7230 §7)
                var sawCommaInLead = false;
                while (index < span.Length && (span[index] is ' ' or '\t' or ','))
                {
                    if (span[index] == ',')
                    {
                        sawCommaInLead = true;
                    }
                    index++;
                }

                if (index >= span.Length)
                {
                    break;
                }

                // Check wildcard '*'
                if (span[index] == '*')
                {
                    // A wildcard cannot be preceded by a comma, repeated, or combined with prior tags
                    if (sawCommaInLead || sawWildcard || strongTags.Count > 0 || weakTags.Count > 0)
                    {
                        return IfMatchCondition.Malformed("Wildcard '*' cannot be repeated or combined with other entity-tags or list separators.");
                    }

                    sawWildcard = true;
                    sawAnyToken = true;
                    index++;

                    // Skip trailing OWS (SP / HTAB)
                    while (index < span.Length && (span[index] is ' ' or '\t'))
                    {
                        index++;
                    }

                    // A wildcard cannot have trailing characters or trailing commas
                    if (index < span.Length)
                    {
                        return IfMatchCondition.Malformed("Wildcard '*' cannot be combined with other entity-tags or list separators.");
                    }

                    break;
                }

                // If we already saw wildcard in a previous header value or position, any further token is invalid
                if (sawWildcard)
                {
                    return IfMatchCondition.Malformed("Wildcard '*' cannot be combined with other entity-tags or list separators.");
                }

                // Check weak tag prefix W/"..."
                var isWeak = false;
                if (index + 2 < span.Length && span[index] == 'W' && span[index + 1] == '/' && span[index + 2] == '"')
                {
                    isWeak = true;
                    index += 2; // Position index at quote
                }

                if (index >= span.Length || span[index] != '"')
                {
                    return IfMatchCondition.Malformed("Entity-tag must be enclosed in double quotes.");
                }

                // Parse opaque-tag: quote to next quote
                var quoteStart = index;
                index++; // move past opening quote
                var quoteEnd = -1;

                while (index < span.Length)
                {
                    var c = span[index];
                    if (c == '"')
                    {
                        quoteEnd = index;
                        index++; // move past closing quote
                        break;
                    }

                    // Check valid etagc: ASCII 0x21, 0x23-0x7E, obs-text (0x80-0xFF).
                    // Space (0x20), controls (< 0x21), and characters > 0xFF are NOT valid etagc.
                    if (c == ' ' || c < 0x21 || (c > 0x7E && c < 0x80) || c > 0xFF)
                    {
                        return IfMatchCondition.Malformed($"Invalid character in entity-tag: '{(c == ' ' ? "<space>" : c.ToString())}'.");
                    }

                    index++;
                }

                if (quoteEnd == -1)
                {
                    return IfMatchCondition.Malformed("Entity-tag is missing closing double quote.");
                }

                var fullTag = isWeak
                    ? "W/" + span.Slice(quoteStart, quoteEnd - quoteStart + 1).ToString()
                    : span.Slice(quoteStart, quoteEnd - quoteStart + 1).ToString();

                if (isWeak)
                {
                    weakTags.Add(fullTag);
                }
                else
                {
                    strongTags.Add(fullTag);
                }

                sawAnyToken = true;

                // Skip OWS after closing quote (SP / HTAB)
                while (index < span.Length && (span[index] is ' ' or '\t'))
                {
                    index++;
                }

                if (index < span.Length)
                {
                    if (span[index] == ',')
                    {
                        index++; // Consume delimiter, continue loop for next tag
                    }
                    else
                    {
                        return IfMatchCondition.Malformed($"Unexpected character after entity-tag at position {index}: '{span[index]}'.");
                    }
                }
            }
        }

        if (!sawAnyToken)
        {
            return IfMatchCondition.Malformed("If-Match header must contain at least one valid entity-tag.");
        }

        if (sawWildcard)
        {
            if (strongTags.Count > 0 || weakTags.Count > 0)
            {
                return IfMatchCondition.Malformed("Wildcard '*' cannot be combined with specific entity-tags.");
            }

            return IfMatchCondition.Wildcard();
        }

        return IfMatchCondition.FromTags(strongTags, weakTags);
    }
}
