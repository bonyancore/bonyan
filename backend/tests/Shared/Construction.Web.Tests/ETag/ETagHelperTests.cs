using Construction.Web.ETag;
using Xunit;

namespace Construction.Web.Tests.ETag;

public sealed class ETagHelperTests
{
    [Fact]
    public void ToETag_ProducesDoubleQuotedDecimalString()
    {
        var result = ETagHelper.ToETag(42u);
        Assert.Equal("\"42\"", result);
    }

    [Fact]
    public void ToETag_Zero_ProducesDoubleQuotedZero()
    {
        var result = ETagHelper.ToETag(0u);
        Assert.Equal("\"0\"", result);
    }

    [Theory]
    [InlineData("\"42\"", true, 42u)]
    [InlineData("\"0\"", true, 0u)]
    [InlineData("42", true, 42u)]
    [InlineData("  \"42\"  ", true, 42u)]
    public void TryParseETag_ValidInputs_ParseCorrectly(string raw, bool expectedResult, uint expectedValue)
    {
        var result = ETagHelper.TryParseETag(raw, out var v);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"abc\"")]
    public void TryParseETag_InvalidInputs_ReturnFalse(string? raw)
    {
        var result = ETagHelper.TryParseETag(raw, out var v);
        Assert.False(result);
        Assert.Equal(0u, v);
    }

    [Fact]
    public void ParseIfMatch_MissingOrNull_ReturnsMissing()
    {
        var res1 = ETagHelper.ParseIfMatch(null);
        Assert.True(res1.IsMissing);

        var res2 = ETagHelper.ParseIfMatch([]);
        Assert.True(res2.IsMissing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ParseIfMatch_WhitespaceOnly_ReturnsMalformed(string raw)
    {
        var res = ETagHelper.ParseIfMatch([raw]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_EmptyOpaqueTag_IsValidSyntax()
    {
        // If-Match: "" is valid empty opaque-tag syntax per RFC 7232
        var res = ETagHelper.ParseIfMatch(["\"\""]);
        Assert.False(res.IsMalformed);
        Assert.False(res.IsMissing);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"\"", res.EntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_TagWithSpaces_IsMalformed()
    {
        // Space 0x20 is not a valid etagc character
        var res = ETagHelper.ParseIfMatch(["\"   \""]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_UnquotedToken_IsMalformed()
    {
        var res = ETagHelper.ParseIfMatch(["42"]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_CommaInsideQuotes_IsPartOfOpaqueTag()
    {
        // If-Match: "10,20" contains comma inside quote, so it's a single tag, not a list
        var res = ETagHelper.ParseIfMatch(["\"10,20\""]);
        Assert.False(res.IsMalformed);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"10,20\"", res.EntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_MultipleTagsInSingleHeader_ParsesAll()
    {
        var res = ETagHelper.ParseIfMatch(["\"10\", \"42\", \"99\""]);
        Assert.False(res.IsMalformed);
        Assert.Equal(3, res.EntityTags.Count);
        Assert.Equal("\"10\"", res.EntityTags[0]);
        Assert.Equal("\"42\"", res.EntityTags[1]);
        Assert.Equal("\"99\"", res.EntityTags[2]);
    }

    [Fact]
    public void ParseIfMatch_MultipleHeaderFieldValues_ParsesAll()
    {
        var res = ETagHelper.ParseIfMatch(["\"10\"", "\"42\""]);
        Assert.False(res.IsMalformed);
        Assert.Equal(2, res.EntityTags.Count);
        Assert.Equal("\"10\"", res.EntityTags[0]);
        Assert.Equal("\"42\"", res.EntityTags[1]);
    }

    [Fact]
    public void ParseIfMatch_WeakTag_ParsesAsWeak()
    {
        var res = ETagHelper.ParseIfMatch(["W/\"42\""]);
        Assert.False(res.IsMalformed);
        Assert.Empty(res.EntityTags);
        Assert.Single(res.WeakEntityTags);
        Assert.Equal("W/\"42\"", res.WeakEntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_WildcardSingle_ParsesAsWildcard()
    {
        var res = ETagHelper.ParseIfMatch(["*"]);
        Assert.False(res.IsMalformed);
        Assert.True(res.IsWildcard);
    }

    [Fact]
    public void ParseIfMatch_WildcardWithSurroundingOws_ParsesAsWildcard()
    {
        var res = ETagHelper.ParseIfMatch([" * \t"]);
        Assert.False(res.IsMalformed);
        Assert.True(res.IsWildcard);
    }

    [Theory]
    [InlineData("*, \"42\"")]
    [InlineData("\"42\", *")]
    [InlineData("*, *")]
    [InlineData("*,")]
    [InlineData(",*")]
    [InlineData(" , *")]
    public void ParseIfMatch_WildcardCombinedWithTagsOrSeparators_IsMalformed(string raw)
    {
        var res = ETagHelper.ParseIfMatch([raw]);
        Assert.True(res.IsMalformed);
    }

    [Theory]
    [InlineData("*", ",")]
    [InlineData("*", " , ")]
    [InlineData("*", "")]
    [InlineData("*", "   ")]
    public void ParseIfMatch_WildcardFollowedByCommasOrEmptyValues_IsMalformed(string first, string second)
    {
        var res = ETagHelper.ParseIfMatch([first, second]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_MultipleWildcardHeaders_ReturnsMalformed()
    {
        var res = ETagHelper.ParseIfMatch(["*", "*"]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_TrailingCommaInEntityTagList_SucceedsPerRFC7230()
    {
        var res = ETagHelper.ParseIfMatch(["\"42\","]);
        Assert.False(res.IsMalformed);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"42\"", res.EntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_ConsecutiveEmptyElementsInTagList_SucceedsPerRFC7230()
    {
        var res = ETagHelper.ParseIfMatch(["\"42\",,"]);
        Assert.False(res.IsMalformed);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"42\"", res.EntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_TagWithUnicodeAbove0xFF_ReturnsMalformed()
    {
        var res = ETagHelper.ParseIfMatch(["\"v1\u0627\""]);
        Assert.True(res.IsMalformed);
    }

    [Fact]
    public void ParseIfMatch_MultipleEntityTagHeaders_ReturnsAllTags()
    {
        var res = ETagHelper.ParseIfMatch(["\"42\"", "\"43\""]);
        Assert.False(res.IsMalformed);
        Assert.Equal(2, res.EntityTags.Count);
        Assert.Equal("\"42\"", res.EntityTags[0]);
        Assert.Equal("\"43\"", res.EntityTags[1]);
    }

    [Fact]
    public void ParseIfMatch_ObsTextInRange_0x80_to_0xFF_Succeeds()
    {
        // 0xC0 (À) and 0xFF (ÿ) are in the obs-text range [0x80, 0xFF]
        var res = ETagHelper.ParseIfMatch(["\"\u00C0\u00FF\""]);
        Assert.False(res.IsMalformed);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"\u00C0\u00FF\"", res.EntityTags[0]);
    }

    [Fact]
    public void ParseIfMatch_StrictOWS_SpAndHtab_Succeeds()
    {
        var res = ETagHelper.ParseIfMatch([" \t\"42\" \t, \t\"43\" \t"]);
        Assert.False(res.IsMalformed);
        Assert.Equal(2, res.EntityTags.Count);
        Assert.Equal("\"42\"", res.EntityTags[0]);
        Assert.Equal("\"43\"", res.EntityTags[1]);
    }

    [Fact]
    public void ParseIfMatch_LeadingEmptyListElements_SucceedsPerRFC7230()
    {
        var res = ETagHelper.ParseIfMatch([",, \"42\""]);
        Assert.False(res.IsMalformed);
        Assert.Single(res.EntityTags);
        Assert.Equal("\"42\"", res.EntityTags[0]);
    }

    [Fact]
    public void ConditionMatches_WeakTag_NeverSatisfiesStrongIfMatch()
    {
        var condition = ETagHelper.ParseIfMatch(["W/\"42\""]);
        Assert.False(condition.Matches("\"42\""));
        Assert.False(condition.Matches("W/\"42\""));
    }

    [Fact]
    public void ConditionMatches_StrongMatch_ReturnsTrue()
    {
        var condition = ETagHelper.ParseIfMatch(["\"10\", \"42\""]);
        Assert.True(condition.Matches("\"42\""));
        Assert.False(condition.Matches("\"99\""));
    }

    [Fact]
    public void ConditionMatches_WeakTag_DoesNotMatchStrong()
    {
        var condition = ETagHelper.ParseIfMatch(["W/\"42\""]);
        Assert.False(condition.Matches("\"42\""));
    }

    [Fact]
    public void ConditionMatches_Wildcard_MatchesAny()
    {
        var condition = ETagHelper.ParseIfMatch(["*"]);
        Assert.True(condition.Matches("\"any_version\""));
    }
}
