using Xunit;

namespace Construction.Kernel.Tests;

public class IdsTests
{
    [Fact]
    public void New_ReturnsNonEmptyGuid()
    {
        var id = Ids.New();

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void New_GeneratesUuidV7WithCorrectVersionAndVariant()
    {
        var id = Ids.New();

        // UUID version 7
        Assert.Equal(7, id.Version);

        // RFC 9562 Leach-Salz variant bit pattern: top two bits of variant byte are 10xx_xxxx (0b1000 to 0b1011, i.e. 8, 9, 10, or 11)
        Assert.InRange(id.Variant, 8, 11);
    }

    [Fact]
    public void New_GeneratesUniqueIdentifiers()
    {
        var set = new HashSet<Guid>();
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            var id = Ids.New();
            Assert.True(set.Add(id), $"Duplicate GUID generated: {id}");
        }

        Assert.Equal(count, set.Count);
    }
}
