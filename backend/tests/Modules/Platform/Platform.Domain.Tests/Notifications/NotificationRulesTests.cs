using Xunit;
using Construction.Platform.Core.Domain.Notifications;

namespace Construction.Platform.Domain.Tests.Notifications;

public sealed class NotificationRulesTests
{
    [Theory]
    [InlineData("22:00", true)]
    [InlineData("03:00", true)]
    [InlineData("06:29", true)]
    [InlineData("06:30", false)]
    [InlineData("12:00", false)]
    public void QuietHours_contains_overnight_window(string time, bool inside)
    {
        var hours = new QuietHours(new TimeOnly(21, 0), new TimeOnly(6, 30));

        Assert.Equal(inside, hours.Contains(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void QuietHours_end_of_window_is_utc_and_next_day_when_overnight()
    {
        var now = DateTimeOffset.Parse(
            "2026-11-21T22:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var end = NotificationRules.QuietHoursEndUtc(
            new QuietHours(new TimeOnly(21, 0), new TimeOnly(6, 30)), now, "Africa/Cairo");

        Assert.NotNull(end);
        Assert.True(end > now);
        Assert.Equal(4, end!.Value.UtcDateTime.Hour);
    }

    [Fact]
    public void QuietHours_returns_null_outside_window()
    {
        var now = DateTimeOffset.Parse(
            "2026-11-21T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var end = NotificationRules.QuietHoursEndUtc(QuietHours.Default, now, "Africa/Cairo");

        Assert.Null(end);
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(2, 10, false)]
    [InlineData(3, 60, false)]
    [InlineData(4, 0, true)]
    public void RetrySchedule_is_one_minute_ten_minutes_one_hour_then_terminal(
        int attempt, int expectedMinutes, bool terminal)
    {
        var (delay, isTerminal) = NotificationRules.RetrySchedule(attempt);

        Assert.Equal(expectedMinutes, (int)delay.TotalMinutes);
        Assert.Equal(terminal, isTerminal);
    }

    [Fact]
    public void Render_encodes_html_substitutions()
    {
        var parameters = new Dictionary<string, object?> { ["name"] = "<script>alert(1)</script>" };

        var rendered = NotificationRules.Render("<p>{{name}}</p>", parameters, html: true);

        Assert.DoesNotContain("<script>", rendered, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_leaves_unknown_placeholders_empty_and_never_executes_expressions()
    {
        var rendered = NotificationRules.Render(
            "{{known}} {{constructor}}",
            new Dictionary<string, object?> { ["known"] = "ok" },
            html: false);

        Assert.Equal("ok ", rendered);
        Assert.DoesNotContain("constructor", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_does_not_evaluate_expression_syntax()
    {
        var rendered = NotificationRules.Render(
            "{{known.toUpper()}}",
            new Dictionary<string, object?> { ["known"] = "ok" },
            html: false);

        // Not a registered placeholder: left inert, never evaluated.
        Assert.Equal("{{known.toUpper()}}", rendered);
    }

    [Theory]
    [InlineData("/projects/1/approvals", true)]
    [InlineData("https://app.example.com/x", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html;base64,AAA", false)]
    [InlineData("//evil.example.com", false)]
    [InlineData("http://insecure.example.com", false)]
    public void ValidateLink_allows_relative_and_https_only(string link, bool valid)
    {
        Assert.Equal(valid, NotificationRules.ValidateLink(link).IsSuccess);
    }

    [Fact]
    public void ChannelAllowed_never_suppresses_mandatory_categories()
    {
        Assert.True(NotificationRules.ChannelAllowed(isMandatory: true, preferenceEnabled: false));
        Assert.False(NotificationRules.ChannelAllowed(isMandatory: false, preferenceEnabled: false));
    }

    [Fact]
    public void DedupeApplies_only_to_recent_unread_items()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(NotificationRules.DedupeApplies(now.AddHours(-1), now, isRead: false));
        Assert.False(NotificationRules.DedupeApplies(now.AddHours(-25), now, isRead: false));
        Assert.False(NotificationRules.DedupeApplies(now.AddHours(-1), now, isRead: true));
    }

    [Fact]
    public void DigestKey_is_stable_per_user_and_local_date()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal(
            NotificationRules.DigestKey(userId, new DateOnly(2026, 11, 21)),
            NotificationRules.DigestKey(userId, new DateOnly(2026, 11, 21)));
    }
}
