using Construction.Kernel.Pagination;
using Xunit;

namespace Construction.Web.Tests.Pagination;

public sealed class PageCursorTests
{
    [Fact]
    public void Encode_ThenTryDecode_RoundTrips()
    {
        var original = new PageCursor(
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Guid.Parse("12345678-1234-1234-1234-123456789012"));

        var encoded = original.Encode();
        var decoded = PageCursor.TryDecode(encoded, out var cursor);

        Assert.True(decoded);
        Assert.NotNull(cursor);
        Assert.Equal(original.CreatedAt, cursor!.CreatedAt);
        Assert.Equal(original.Id, cursor.Id);
    }

    [Fact]
    public void Encode_IsUrlSafe()
    {
        var cursor = new PageCursor(DateTime.UtcNow, Guid.NewGuid());
        var encoded = cursor.Encode();

        // Base64url must not contain standard Base64 characters that need URL-encoding
        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-cursor")]
    [InlineData("YQ==")]  // 1-byte payload, too short
    public void TryDecode_InvalidInputs_ReturnFalse(string? input)
    {
        var result = PageCursor.TryDecode(input, out var cursor);
        Assert.False(result);
        Assert.Null(cursor);
    }

    [Fact]
    public void TryDecode_TruncatedPayload_ReturnFalse()
    {
        // 10-byte payload (too short for 24-byte cursor)
        var shortBytes = new byte[10];
        var truncated = Convert.ToBase64String(shortBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var result = PageCursor.TryDecode(truncated, out var cursor);
        Assert.False(result);
        Assert.Null(cursor);
    }

    [Fact]
    public void Encode_DifferentTimestamps_ProduceDifferentCursors()
    {
        var id = Guid.NewGuid();
        var c1 = new PageCursor(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), id);
        var c2 = new PageCursor(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), id);

        Assert.NotEqual(c1.Encode(), c2.Encode());
    }

    [Fact]
    public void Encode_DifferentIds_ProduceDifferentCursors()
    {
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var c1 = new PageCursor(ts, Guid.NewGuid());
        var c2 = new PageCursor(ts, Guid.NewGuid());

        Assert.NotEqual(c1.Encode(), c2.Encode());
    }
}
