using Xunit;
using System.Text;
using Construction.Web.Idempotency;

namespace Construction.Web.Tests.Idempotency;

public sealed class RequestHasherTests
{
    [Fact]
    public void Hash_ignores_property_order_and_whitespace()
    {
        var a = Hash("""{"b":2,"a":1}""");
        var b = Hash("{ \"a\" : 1, \"b\" : 2 }");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_normalises_equivalent_numbers()
    {
        Assert.Equal(Hash("""{"a":1}"""), Hash("""{"a":1.0}"""));
    }

    [Fact]
    public void Hash_preserves_array_order()
    {
        Assert.NotEqual(Hash("""{"a":[1,2]}"""), Hash("""{"a":[2,1]}"""));
    }

    [Fact]
    public void Hash_changes_with_value_route_and_method()
    {
        var body = """{"a":1}""";

        Assert.NotEqual(Hash(body), Hash("""{"a":2}"""));
        Assert.NotEqual(
            RequestHasher.Compute("POST", "/api/v1/a", Bytes(body), "application/json"),
            RequestHasher.Compute("POST", "/api/v1/b", Bytes(body), "application/json"));
        Assert.NotEqual(
            RequestHasher.Compute("POST", "/api/v1/a", Bytes(body), "application/json"),
            RequestHasher.Compute("PUT", "/api/v1/a", Bytes(body), "application/json"));
    }

    [Fact]
    public void Hash_treats_non_json_body_as_raw_bytes()
    {
        var a = RequestHasher.Compute("POST", "/x", Bytes("a=1&b=2"), "application/x-www-form-urlencoded");
        var b = RequestHasher.Compute("POST", "/x", Bytes("b=2&a=1"), "application/x-www-form-urlencoded");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash_comparison_is_length_safe()
    {
        Assert.True(RequestHasher.HashesEqual([1, 2, 3], [1, 2, 3]));
        Assert.False(RequestHasher.HashesEqual([1, 2, 3], [1, 2]));
        Assert.False(RequestHasher.HashesEqual([1, 2, 3], [1, 2, 4]));
    }

    [Fact]
    public void Key_fingerprint_does_not_contain_the_key()
    {
        var fingerprint = RequestHasher.ToKeyFingerprint("super-secret-key");

        Assert.DoesNotContain("super", fingerprint, StringComparison.Ordinal);
        Assert.Equal(16, fingerprint.Length);
    }

    private static byte[] Hash(string json) =>
        RequestHasher.Compute("POST", "/api/v1/x", Bytes(json), "application/json");

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
