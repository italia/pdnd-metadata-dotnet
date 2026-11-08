// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using FluentAssertions;
using Pdnd.Metadata.Extraction.Jwt;

namespace Pdnd.Metadata.Tests.Jwt;

public class JwtJsonReaderNonObjectRootTests
{
    public static TheoryData<string> NonObjectRoots => new()
    {
        "[]",
        "[{\"iss\":\"x\"}]",
        "\"text\"",
        "42",
        "true",
        "null"
    };

    [Theory]
    [MemberData(nameof(NonObjectRoots))]
    public void TryReadString_ShouldReturnFalse_WhenRootIsNotObject(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var act = () => JwtJsonReader.TryReadString(doc.RootElement, "iss", out _);

        act.Should().NotThrow();
        JwtJsonReader.TryReadString(doc.RootElement, "iss", out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NonObjectRoots))]
    public void TryReadAudience_ShouldReturnFalse_WhenRootIsNotObject(string json)
    {
        using var doc = JsonDocument.Parse(json);

        JwtJsonReader.TryReadAudience(doc.RootElement, out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NonObjectRoots))]
    public void TryReadSignedHeaders_ShouldReturnFalse_WhenRootIsNotObject(string json)
    {
        using var doc = JsonDocument.Parse(json);

        JwtJsonReader.TryReadSignedHeaders(doc.RootElement, out var pairs).Should().BeFalse();
        pairs.Should().BeEmpty();
    }
}
