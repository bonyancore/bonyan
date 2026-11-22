using System.Globalization;
using System.Text.RegularExpressions;
using Construction.Kernel;

namespace Construction.Platform.Core.Domain.NumberSeries;

public enum NumberScope
{
    Tenant,
    Project,
    Year,
    ProjectYear
}

/// <summary>Current persisted state of a series, as needed by the pure locking rules.</summary>
public sealed record NumberSeriesDefinition(
    string Code,
    string Prefix,
    NumberScope Scope,
    string Format,
    int Padding,
    long StartValue,
    bool IsLocked,
    long LastIssued);

/// <summary>Requested changes to a series (null = leave unchanged).</summary>
public sealed record NumberSeriesChange(
    string? Prefix = null,
    NumberScope? Scope = null,
    string? Format = null,
    int? Padding = null,
    long? StartValue = null);

/// <summary>
/// Pure number-series rules from spec 01 §4.9 / BR-PLT-20…24. Formatting, scope keys and the
/// is_locked invariants live here so they are unit-testable without a database.
/// </summary>
public static partial class NumberSeriesRules
{
    public const int MinPadding = 1;
    public const int MaxPadding = 10;
    public const int MaxPrefixLength = 10;
    public const string DefaultTimeZone = "Africa/Cairo";

    private static readonly string[] Tokens = ["{prefix}", "{yyyy}", "{project}", "{seq}"];

    public static Result<string> ValidateFormat(string? format, int padding)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                "A number format is required."));
        }

        if (padding is < MinPadding or > MaxPadding)
        {
            return Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                $"Padding must be between {MinPadding} and {MaxPadding}."));
        }

        var remainder = format;
        foreach (var token in Tokens)
        {
            remainder = remainder.Replace(token, string.Empty, StringComparison.Ordinal);
        }

        if (remainder.Contains('{', StringComparison.Ordinal) ||
            remainder.Contains('}', StringComparison.Ordinal))
        {
            return Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                "The format contains an unknown token."));
        }

        var sequenceTokens = CountOccurrences(format, "{seq}");
        if (sequenceTokens != 1)
        {
            return Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                "The format must contain {seq} exactly once."));
        }

        return Result.Success(format);
    }

    public static Result<string> ValidatePrefix(string? prefix)
    {
        if (prefix is null || !PrefixPattern().IsMatch(prefix))
        {
            return Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                $"The prefix must match [A-Za-z0-9_-]{{0,{MaxPrefixLength}}}."));
        }

        return Result.Success(prefix);
    }

    public static string Render(
        string format,
        string prefix,
        long sequence,
        int padding,
        string? year,
        string? project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var sequenceText = sequence.ToString(
            padding <= 0 ? "D1" : "D" + padding.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        return format
            .Replace("{prefix}", prefix, StringComparison.Ordinal)
            .Replace("{yyyy}", year ?? string.Empty, StringComparison.Ordinal)
            .Replace("{project}", project ?? string.Empty, StringComparison.Ordinal)
            .Replace("{seq}", sequenceText, StringComparison.Ordinal);
    }

    public static Result<string> ScopeKey(
        NumberScope scope,
        Guid? projectId,
        string? projectCode,
        DateTimeOffset atUtc,
        string? timeZoneId)
    {
        var year = YearInTimeZone(atUtc, timeZoneId);

        return scope switch
        {
            NumberScope.Tenant => Result.Success("-"),
            NumberScope.Project => projectId is null || projectId == Guid.Empty
                ? Result.Failure<string>(Error.Validation(
                    "platform.number_project_required",
                    "This series requires a project."))
                : Result.Success(projectId.Value.ToString("N", CultureInfo.InvariantCulture)),
            NumberScope.Year => Result.Success(year),
            NumberScope.ProjectYear => projectId is null || projectId == Guid.Empty
                ? Result.Failure<string>(Error.Validation(
                    "platform.number_project_required",
                    "This series requires a project."))
                : Result.Success(
                    $"{projectId.Value.ToString("N", CultureInfo.InvariantCulture)}:{year}"),
            _ => Result.Failure<string>(Error.Validation(
                "platform.number_format_invalid",
                "Unknown number scope.")),
        };
    }

    /// <summary>
    /// Year of the instant in the tenant's time zone (BR-PLT-21 / T-206-U03). A 2026-12-31T22:30Z
    /// instant is 2027 in Africa/Cairo.
    /// </summary>
    public static string YearInTimeZone(DateTimeOffset atUtc, string? timeZoneId)
    {
        TimeZoneInfo zone;
        try
        {
            zone = string.IsNullOrWhiteSpace(timeZoneId)
                ? TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone)
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone);
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone);
        }

        var local = TimeZoneInfo.ConvertTime(atUtc, zone);
        return local.Year.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// BR-PLT-22: after the first allocation the prefix, scope, format and start value are
    /// immutable; padding may only increase. Before any allocation a higher start is allowed,
    /// a start at or below the last issued value is a conflict (BR-PLT-23).
    /// </summary>
    public static Error? ValidateChange(NumberSeriesDefinition current, NumberSeriesChange change)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(change);

        if (change.Padding is { } padding && padding is < MinPadding or > MaxPadding)
        {
            return Error.Validation("platform.number_format_invalid", "Padding must be between 1 and 10.");
        }

        if (change.Prefix is not null)
        {
            var prefix = ValidatePrefix(change.Prefix);
            if (prefix.IsFailure)
            {
                return prefix.Error;
            }
        }

        if (change.Format is not null)
        {
            var format = ValidateFormat(change.Format, change.Padding ?? current.Padding);
            if (format.IsFailure)
            {
                return format.Error;
            }
        }

        if (current.IsLocked)
        {
            if (change.Prefix is not null && !string.Equals(change.Prefix, current.Prefix, StringComparison.Ordinal))
            {
                return Error.Conflict("platform.series_locked", "The series prefix is locked.");
            }

            if (change.Scope is not null && change.Scope != current.Scope)
            {
                return Error.Conflict("platform.series_locked", "The series scope is locked.");
            }

            if (change.Format is not null && !string.Equals(change.Format, current.Format, StringComparison.Ordinal))
            {
                return Error.Conflict("platform.series_locked", "The series format is locked.");
            }

            if (change.StartValue is not null && change.StartValue != current.StartValue)
            {
                return Error.Conflict("platform.series_locked", "The series start value is locked.");
            }

            if (change.Padding is { } newPadding && newPadding < current.Padding)
            {
                return Error.Conflict("platform.series_locked", "Padding may only increase.");
            }
        }
        else if (change.StartValue is { } start && start <= current.LastIssued)
        {
            return Error.Conflict(
                "platform.series_start_conflict",
                "The new start value conflicts with numbers already issued.");
        }

        return null;
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{0,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixPattern();
}
