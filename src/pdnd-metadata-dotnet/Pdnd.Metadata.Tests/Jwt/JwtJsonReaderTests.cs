// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using FluentAssertions;
using Pdnd.Metadata.Extraction.Jwt;

namespace Pdnd.Metadata.Tests.Jwt;

public class JwtJsonReaderTests
{
    [Fact]
    public void TryReadString_ShouldReadStringProperty()
    {
        using var doc = JsonDocument.Parse("{\"iss\":\"issuer\"}");
        JwtJsonReader.TryReadString(doc.RootElement, "iss", out var value).Should().BeTrue();
        value.Should().Be("issuer");
    }

    [Fact]
    public void TryReadString_ShouldReadNumberProperty_AsRawText()
    {
        using var doc = JsonDocument.Parse("{\"iat\":1700000000}");
        JwtJsonReader.TryReadString(doc.RootElement, "iat", out var value).Should().BeTrue();
        value.Should().Be("1700000000");
    }

    [Fact]
    public void TryReadString_ShouldReturnFalse_WhenMissingOrEmpty()
    {
        using var doc = JsonDocument.Parse("{\"iss\":\"\"}");
        JwtJsonReader.TryReadString(doc.RootElement, "missing", out _).Should().BeFalse();
        JwtJsonReader.TryReadString(doc.RootElement, "iss", out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadAudience_ShouldReadStringAud()
    {
        using var doc = JsonDocument.Parse("{\"aud\":\"api\"}");
        JwtJsonReader.TryReadAudience(doc.RootElement, out var value).Should().BeTrue();
        value.Should().Be("api");
    }

    [Fact]
    public void TryReadAudience_ShouldReadArrayAud_AsCommaSeparated()
    {
        using var doc = JsonDocument.Parse("{\"aud\":[\"a\",\"b\"]}");
        JwtJsonReader.TryReadAudience(doc.RootElement, out var value).Should().BeTrue();
        value.Should().Be("a, b");
    }

    [Fact]
    public void TryReadAudience_ShouldReturnFalse_WhenAudMissingOrInvalid()
    {
        using var doc = JsonDocument.Parse("{\"aud\":123}");
        JwtJsonReader.TryReadAudience(doc.RootElement, out _).Should().BeFalse();
    }
}