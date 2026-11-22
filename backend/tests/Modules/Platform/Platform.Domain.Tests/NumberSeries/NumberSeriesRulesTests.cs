using Xunit;
using Construction.Platform.Core.Domain.NumberSeries;

namespace Construction.Platform.Domain.Tests.NumberSeries;

public sealed class NumberSeriesRulesTests
{
    [Theory]
    [InlineData("{prefix}-{seq}", "PO", 1066, 4, "PO-1066")]
    [InlineData("{prefix}-{seq}", "PA", 4, 2, "PA-04")]
    [InlineData("{prefix}-{seq}", "PO", 10000, 4, "PO-10000")]
    [InlineData("{prefix}/{yyyy}/{seq}", "INV", 7, 4, "INV/2026/0007")]
    public void Render_formats_sequence_and_tokens(
        string format, string prefix, long sequence, int padding, string expected)
    {
        var actual = NumberSeriesRules.Render(
            format, prefix: prefix, sequence, padding, "2026", null);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Render_orders_project_and_year_tokens()
    {
        var actual = NumberSeriesRules.Render(
            "{prefix}-{project}-{yyyy}-{seq}", "PA", 5, 2, "2026", "PRJ");

        Assert.Equal("PA-PRJ-2026-05", actual);
    }

    [Theory]
    [InlineData("PO-{seq}", 4, true)]
    [InlineData("{prefix}-{yyyy}-{seq}", 4, true)]
    [InlineData("PO-0001", 4, false)]
    [InlineData("{prefix}-{seq}-{seq}", 4, false)]
    [InlineData("{prefix}-{seq}-{unknown}", 4, false)]
    [InlineData("{prefix}-{seq", 4, false)]
    [InlineData("{prefix}-{seq}", 0, false)]
    [InlineData("{prefix}-{seq}", 11, false)]
    public void ValidateFormat_rejects_missing_duplicate_and_unknown_tokens(
        string format, int padding, bool valid)
    {
        var result = NumberSeriesRules.ValidateFormat(format, padding);

        Assert.Equal(valid, result.IsSuccess);
    }

    [Theory]
    [InlineData("PO", true)]
    [InlineData("", true)]
    [InlineData("PO_2026-x", true)]
    [InlineData("PO 1", false)]
    [InlineData("PO;DROP", false)]
    [InlineData("ABCDEFGHIJK", false)]
    public void ValidatePrefix_enforces_charset_and_length(string prefix, bool valid)
    {
        Assert.Equal(valid, NumberSeriesRules.ValidatePrefix(prefix).IsSuccess);
    }

    [Fact]
    public void ScopeKey_uses_tenant_local_year_at_the_cairo_boundary()
    {
        var instant = DateTimeOffset.Parse(
            "2026-12-31T22:30:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var key = NumberSeriesRules.ScopeKey(
            NumberScope.Year, null, null, instant, "Africa/Cairo");

        Assert.True(key.IsSuccess);
        Assert.Equal("2027", key.Value);
    }

    [Fact]
    public void ScopeKey_requires_a_project_for_project_scope()
    {
        var key = NumberSeriesRules.ScopeKey(
            NumberScope.Project, null, null, DateTimeOffset.UtcNow, "Africa/Cairo");

        Assert.True(key.IsFailure);
        Assert.Equal("platform.number_project_required", key.Error.Code);
    }

    [Fact]
    public void ValidateChange_allows_padding_increase_after_lock()
    {
        var current = Locked();

        var error = NumberSeriesRules.ValidateChange(
            current, new NumberSeriesChange(Padding: 6));

        Assert.Null(error);
    }

    [Fact]
    public void ValidateChange_rejects_padding_decrease_after_lock()
    {
        var error = NumberSeriesRules.ValidateChange(
            Locked(), new NumberSeriesChange(Padding: 3));

        Assert.NotNull(error);
        Assert.Equal("platform.series_locked", error.Code);
    }

    [Theory]
    [InlineData("XX", null, null)]
    [InlineData(null, NumberScope.Project, null)]
    [InlineData(null, null, "{prefix}-{yyyy}-{seq}")]
    [InlineData(null, null, null, 5L)]
    public void ValidateChange_rejects_immutable_fields_after_lock(
        string? prefix, NumberScope? scope, string? format, long? start = null)
    {
        var error = NumberSeriesRules.ValidateChange(
            Locked(), new NumberSeriesChange(prefix, scope, format, StartValue: start));

        Assert.NotNull(error);
        Assert.Equal("platform.series_locked", error.Code);
    }

    [Fact]
    public void ValidateChange_rejects_start_at_or_below_last_issued_before_lock()
    {
        var current = Locked() with { IsLocked = false };

        var error = NumberSeriesRules.ValidateChange(current, new NumberSeriesChange(StartValue: 100));

        Assert.NotNull(error);
        Assert.Equal("platform.series_start_conflict", error.Code);
    }

    private static NumberSeriesDefinition Locked() =>
        new("PO", "PO", NumberScope.Tenant, "{prefix}-{seq}", 4, 1, IsLocked: true, LastIssued: 100);
}
