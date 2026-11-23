using Construction.Kernel.Identity;
using Xunit;

namespace Construction.Kernel.Tests;

public class IdentityIdTests
{
    [Fact]
    public void UserId_Constructor_WithGuidEmpty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new UserId(Guid.Empty));
        Assert.Contains("UserId cannot be empty", ex.Message);
    }

    [Fact]
    public void UserId_From_WithValidGuid_CreatesUserId()
    {
        var guid = Guid.NewGuid();
        var userId = UserId.From(guid);

        Assert.Equal(guid, userId.Value);
        Assert.True(userId.IsValid);
        Assert.Equal(guid.ToString(), userId.ToString());
    }

    [Fact]
    public void UserId_New_GeneratesValidVersion7Guid()
    {
        var userId = UserId.New();
        Assert.True(userId.IsValid);
        Assert.NotEqual(Guid.Empty, userId.Value);

        // Verify UUID version 7: byte 7 (in big-endian/standard UUID byte order) has version 7 in upper 4 bits
        // .NET Guid.ToByteArray() returns endian-swapped bytes for first 3 components,
        // but bytes 6, 7, 8..15 match UUID standard byte layout, where byte 7 is version or Guid.Version property in .NET 9+.
        var bytes = userId.Value.ToByteArray();
        var version = (bytes[7] >> 4) & 0x0F;
        Assert.Equal(7, version);
    }

    [Fact]
    public void UserId_Equality_And_Inequality_WorkCorrectly()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var id1 = UserId.From(guid1);
        var id1Copy = UserId.From(guid1);
        var id2 = UserId.From(guid2);

        Assert.Equal(id1, id1Copy);
        Assert.True(id1 == id1Copy);
        Assert.False(id1 != id1Copy);

        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id1Copy.GetHashCode());
    }

    [Fact]
    public void UserId_DefaultStruct_IsValid_ReturnsFalse()
    {
        var defaultId = default(UserId);

        Assert.False(defaultId.IsValid);
        Assert.Equal(Guid.Empty, defaultId.Value);

        var ex = Assert.Throws<InvalidOperationException>(() => defaultId.EnsureValid());
        Assert.Contains("UserId is not initialized", ex.Message);
    }

    [Fact]
    public void UserId_ImplicitConversion_And_Deconstruction_Work()
    {
        var guid = Guid.NewGuid();
        var userId = new UserId(guid);

        Guid implicitGuid = userId;
        Assert.Equal(guid, implicitGuid);

        userId.Deconstruct(out var outVal);
        Assert.Equal(guid, outVal);

        var explicitUser = (UserId)guid;
        Assert.Equal(userId, explicitUser);
    }

    [Fact]
    public void RefreshTokenId_Constructor_WithGuidEmpty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RefreshTokenId(Guid.Empty));
        Assert.Contains("RefreshTokenId cannot be empty", ex.Message);
    }

    [Fact]
    public void RefreshTokenId_From_WithValidGuid_CreatesRefreshTokenId()
    {
        var guid = Guid.NewGuid();
        var tokenId = RefreshTokenId.From(guid);

        Assert.Equal(guid, tokenId.Value);
        Assert.True(tokenId.IsValid);
        Assert.Equal(guid.ToString(), tokenId.ToString());
    }

    [Fact]
    public void RefreshTokenId_New_GeneratesValidVersion7Guid()
    {
        var tokenId = RefreshTokenId.New();
        Assert.True(tokenId.IsValid);
        Assert.NotEqual(Guid.Empty, tokenId.Value);

        var bytes = tokenId.Value.ToByteArray();
        var version = (bytes[7] >> 4) & 0x0F;
        Assert.Equal(7, version);
    }

    [Fact]
    public void RefreshTokenId_Equality_And_Inequality_WorkCorrectly()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var id1 = RefreshTokenId.From(guid1);
        var id1Copy = RefreshTokenId.From(guid1);
        var id2 = RefreshTokenId.From(guid2);

        Assert.Equal(id1, id1Copy);
        Assert.True(id1 == id1Copy);
        Assert.False(id1 != id1Copy);

        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id1Copy.GetHashCode());
    }

    [Fact]
    public void RefreshTokenId_DefaultStruct_IsValid_ReturnsFalse()
    {
        var defaultId = default(RefreshTokenId);

        Assert.False(defaultId.IsValid);
        Assert.Equal(Guid.Empty, defaultId.Value);

        var ex = Assert.Throws<InvalidOperationException>(() => defaultId.EnsureValid());
        Assert.Contains("RefreshTokenId is not initialized", ex.Message);
    }
}
