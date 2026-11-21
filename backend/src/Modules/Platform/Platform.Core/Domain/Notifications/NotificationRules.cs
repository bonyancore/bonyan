using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Construction.Kernel;

namespace Construction.Platform.Core.Domain.Notifications;

public sealed record QuietHours(TimeOnly From, TimeOnly To)
{
    public static QuietHours Default { get; } = new(new TimeOnly(21, 0), new TimeOnly(6, 30));

    /// <summary>Overnight windows and equal endpoints (all day) are supported.</summary>
    public bool Contains(TimeOnly localTime)
    {
        if (From == To)
        {
            return true;
        }

        return From < To
            ? localTime >= From && localTime < To
            : localTime >= From || localTime < To;
    }
}

/// <summary>
/// Notification policy rules (spec 01 §6.11 / BR-PLT-110…117). Pure functions so quiet hours,
/// dedupe, mandatory-channel and retry behavior are unit-testable without a database.
/// </summary>
public static partial class NotificationRules
{
    public static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(24);

    public static readonly string[] MandatoryCategories = ["approval", "security"];

    public static bool IsMandatory(string? category) =>
        category is not null && MandatoryCategories.Contains(category, StringComparer.OrdinalIgnoreCase);

    /// <summary>Mandatory templates cannot be disabled by a preference (BR-PLT-112).</summary>
    public static bool ChannelAllowed(bool isMandatory, bool preferenceEnabled) =>
        isMandatory || preferenceEnabled;

    /// <summary>+1 min, +10 min, +1 h, then terminal (BR-PLT-115).</summary>
    public static (TimeSpan Delay, bool Terminal) RetrySchedule(int attempt) =>
        attempt switch
        {
            1 => (TimeSpan.FromMinutes(1), false),
            2 => (TimeSpan.FromMinutes(10), false),
            3 => (TimeSpan.FromHours(1), false),
            _ => (TimeSpan.Zero, true)
        };

    /// <summary>
    /// End of the quiet-hours window as a UTC instant, or null when now is outside the window.
    /// Uses the tenant/user time zone and is DST-aware through <see cref="TimeZoneInfo"/>.
    /// </summary>
    public static DateTimeOffset? QuietHoursEndUtc(
        QuietHours? hours,
        DateTimeOffset nowUtc,
        string? timeZoneId)
    {
        if (hours is null)
        {
            return null;
        }

        var zone = ResolveZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var localTime = TimeOnly.FromDateTime(local.DateTime);

        if (!hours.Contains(localTime))
        {
            return null;
        }

        var targetDate = local.Date;
        if (hours.From >= hours.To)
        {
            targetDate = localTime >= hours.From ? local.Date.AddDays(1) : local.Date;
        }

        var target = new DateTime(
            targetDate.Year,
            targetDate.Month,
            targetDate.Day,
            hours.To.Hour,
            hours.To.Minute,
            0,
            DateTimeKind.Unspecified);

        var offset = zone.GetUtcOffset(target);
        return new DateTimeOffset(target, offset).ToUniversalTime();
    }

    /// <summary>
    /// Safe deep links only: an application-relative path or an https URL. Script/data schemes and
    /// protocol-relative URLs are rejected.
    /// </summary>
    public static Result<string> ValidateLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return Result.Success(string.Empty);
        }

        if (link.StartsWith('/') && !link.StartsWith("//", StringComparison.Ordinal))
        {
            return Result.Success(link);
        }

        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(link);
        }

        return Result.Failure<string>(Error.Validation(
            "platform.notification_link_invalid", "The notification link is not allowed."));
    }

    /// <summary>
    /// Replaces registered <c>{{key}}</c> placeholders only. No expressions, no nested evaluation.
    /// HTML rendering encodes every substitution.
    /// </summary>
    public static string Render(
        string template,
        IReadOnlyDictionary<string, object?> parameters,
        bool html)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        return PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!parameters.TryGetValue(key, out var value) || value is null)
            {
                return string.Empty;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return html ? WebUtility.HtmlEncode(text) : text;
        });
    }

    public static IReadOnlyList<string> MissingParameters(
        IEnumerable<string> required,
        IReadOnlyDictionary<string, object?> parameters) =>
        required.Where(key => !parameters.ContainsKey(key) || parameters[key] is null).ToArray();

    public static string DigestKey(Guid userId, DateOnly localDate) =>
        string.Create(CultureInfo.InvariantCulture, $"digest:{userId:N}:{localDate:yyyy-MM-dd}");

    public static bool DedupeApplies(DateTimeOffset createdAtUtc, DateTimeOffset nowUtc, bool isRead) =>
        !isRead && nowUtc - createdAtUtc <= DedupeWindow;

    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId)
                ? TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
    }

    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
