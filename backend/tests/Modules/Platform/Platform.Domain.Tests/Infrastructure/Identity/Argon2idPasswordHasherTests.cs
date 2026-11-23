using Construction.Platform.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Construction.Platform.Domain.Tests.Infrastructure.Identity;

public sealed class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher;

    public Argon2idPasswordHasherTests()
    {
        // Use fast parameters for tests
        var options = Options.Create(new IdentityOptions
        {
            Argon2MemoryCostKiB = 1024, // 1MB fast test
            Argon2TimeCost = 1,
            Argon2Parallelism = 1
        });
        _hasher = new Argon2idPasswordHasher(options);
    }

    [Fact]
    public void HashPassword_ValidPassword_ReturnsValidMcfString()
    {
        var password = "SuperSecretPassword123!";
        var hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.StartsWith("$argon2id$v=19$", hash);
        Assert.True(_hasher.VerifyPassword(password, hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        Assert.False(_hasher.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void VerifyPassword_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(_hasher.VerifyPassword("", "somehash"));
        Assert.False(_hasher.VerifyPassword("pass", ""));
    }

    [Fact]
    public void VerifyDummy_ExecutesWithoutExceptionAndReturnsFalse()
    {
        var result = _hasher.VerifyDummy();
        Assert.False(result);
    }
}
