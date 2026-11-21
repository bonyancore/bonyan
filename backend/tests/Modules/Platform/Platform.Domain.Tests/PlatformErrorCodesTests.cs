using Xunit;
using System.Reflection;
using Construction.Platform.Contracts;

namespace Construction.Platform.Domain.Tests;

/// <summary>T-CC-U01: every platform error code carries the prefix and bilingual text.</summary>
public sealed class PlatformErrorCodesTests
{
    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();

        foreach (var field in typeof(PlatformErrorCodes)
                     .GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string)))
        {
            data.Add((string)field.GetRawConstantValue()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void Every_code_has_the_platform_prefix_and_messages(string code)
    {
        Assert.StartsWith("platform.", code, StringComparison.Ordinal);
        Assert.Matches("^platform\\.[a-z0-9_]+$", code);
        Assert.True(PlatformErrorCodes.Messages.ContainsKey(code), $"Missing message for {code}.");
        Assert.False(string.IsNullOrWhiteSpace(PlatformErrorCodes.English(code)));
        Assert.False(string.IsNullOrWhiteSpace(PlatformErrorCodes.Arabic(code)));
    }

    [Fact]
    public void Dead_letter_resolved_uses_the_spec_code()
    {
        Assert.Equal("platform.dead_letter_resolved", PlatformErrorCodes.DeadLetterResolved);
    }
}
