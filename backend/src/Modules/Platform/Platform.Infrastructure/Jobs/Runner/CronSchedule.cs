using System.Globalization;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// Minimal cron support for scheduled jobs: only <c>"M H * * *"</c> with a numeric minute and hour.
/// Evaluated in the tenant's time zone (spec 01 §4.14: the schedule is tenant-local).
/// Any other expression is rejected with <see cref="NotSupportedException"/>; adding a full cron
/// package needs an approved RFC.
/// </summary>
public sealed record CronSchedule(int Minute, int Hour)
{
    private static readonly char[] Separator = [' '];

    public static CronSchedule Parse(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new NotSupportedException("A cron expression is required.");
        }

        var fields = expression.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            throw new NotSupportedException(
                $"Cron '{expression}' must have exactly 5 fields; only 'M H * * *' is supported.");
        }

        if (fields[2] != "*" || fields[3] != "*" || fields[4] != "*")
        {
            throw new NotSupportedException(
                $"Cron '{expression}' is not supported: only day-of-month, month and day-of-week '*' are allowed.");
        }

        if (!int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || minute > 59)
        {
            throw new NotSupportedException(
                $"Cron '{expression}' minute must be a number 0-59 (lists, ranges and steps are not supported).");
        }

        if (!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || hour > 23)
        {
            throw new NotSupportedException(
                $"Cron '{expression}' hour must be a number 0-23 (lists, ranges and steps are not supported).");
        }

        return new CronSchedule(minute, hour);
    }

    /// <summary>
    /// Start of the local window this schedule fires in for the given instant, expressed in UTC.
    /// Used as the idempotency key so one window executes at most once (BR-PLT-140).
    /// </summary>
    public DateTimeOffset WindowStart(DateTimeOffset nowUtc, string? timeZoneId)
    {
        var zone = ResolveZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var localWindow = new DateTimeOffset(
            local.Year,
            local.Month,
            local.Day,
            Hour,
            Minute,
            0,
            local.Offset);

        return localWindow.ToUniversalTime();
    }

    public bool IsDue(DateTimeOffset nowUtc, string? timeZoneId)
    {
        var zone = ResolveZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        return local.Hour == Hour && local.Minute == Minute;
    }

    private static TimeZoneInfo ResolveZone(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
}
