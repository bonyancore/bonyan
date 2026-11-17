using Construction.Web.Filters;
using Xunit;

namespace Construction.Web.Tests.Filters;

/// <summary>
/// A concrete subclass of <see cref="QueryFilterOptions"/> for use in tests.
/// </summary>
file sealed record TestFilterOptions : QueryFilterOptions
{
    protected override IReadOnlySet<string> AllowedSortFields { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "name", "createdAt", "status" };
}

public sealed class QueryFilterOptionsTests
{
    [Fact]
    public void Validate_DefaultOptions_IsValid()
    {
        var options = new TestFilterOptions();
        var result = options.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public void Validate_ValidPageSize_IsValid(int pageSize)
    {
        var options = new TestFilterOptions { PageSize = pageSize };
        var result = options.Validate();

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    [InlineData(int.MaxValue)]
    public void Validate_InvalidPageSize_ReturnsError(int pageSize)
    {
        var options = new TestFilterOptions { PageSize = pageSize };
        var result = options.Validate();

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("pageSize", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("createdAt")]
    [InlineData("status")]
    public void Validate_AllowedSortField_IsValid(string sortField)
    {
        var options = new TestFilterOptions { SortField = sortField };
        var result = options.Validate();

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("unknown_field")]
    [InlineData("'; DROP TABLE--")]
    public void Validate_DisallowedSortField_ReturnsError(string sortField)
    {
        var options = new TestFilterOptions { SortField = sortField };
        var result = options.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sortField", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public void Validate_ValidSortDirection_IsValid(string direction)
    {
        var options = new TestFilterOptions { SortDirection = direction };
        var result = options.Validate();

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("ASC")]       // case-sensitive — must be lowercase
    [InlineData("ascending")]
    [InlineData("1")]
    [InlineData("")]
    public void Validate_InvalidSortDirection_ReturnsError(string direction)
    {
        var options = new TestFilterOptions { SortDirection = direction };
        var result = options.Validate();

        if (string.IsNullOrEmpty(direction))
        {
            // Empty string treated as absent — valid
            Assert.True(result.IsValid);
        }
        else
        {
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("sortDirection", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Validate_MultipleViolations_ReturnsAllErrors()
    {
        var options = new TestFilterOptions
        {
            PageSize = 0,
            SortField = "unknown",
            SortDirection = "ASCENDING",
        };
        var result = options.Validate();

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_WhitespaceOnlyCursor_ReturnsValidationError(string whitespaceCursor)
    {
        var options = new TestFilterOptions { After = whitespaceCursor };
        var result = options.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cursor is invalid or malformed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NullSortFieldAndDirection_IsValid()
    {
        var options = new TestFilterOptions { SortField = null, SortDirection = null };
        var result = options.Validate();

        Assert.True(result.IsValid);
    }
}
