// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction.Pdnd;

namespace Pdnd.Metadata.Tests.Pdnd;

public class DigestParserTests
{
    [Fact]
    public void TryParseDigestHeader_ShouldParseSinglePair()
    {
        DigestParser.TryParseDigestHeader("SHA-256=abc", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("SHA-256");
        val.Should().Be("abc");
    }

    [Fact]
    public void TryParseDigestHeader_ShouldParseQuotedValue()
    {
        DigestParser.TryParseDigestHeader("SHA-256=\"abc==\"", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("SHA-256");
        val.Should().Be("abc==");
    }

    [Fact]
    public void TryParseDigestHeader_ShouldPickFirstValidPair_FromMultiple()
    {
        DigestParser.TryParseDigestHeader("bad, SHA-256=abc, SHA-512=def", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("SHA-256");
        val.Should().Be("abc");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noequals")]
    [InlineData("=noval")]
    [InlineData("alg=")]
    public void TryParseDigestHeader_ShouldReturnFalse_ForInvalid(string? input)
    {
        DigestParser.TryParseDigestHeader(input!, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseContentDigestHeader_ShouldParseRfc9530Format()
    {
        DigestParser.TryParseContentDigestHeader("sha-256=:abc123=:", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("sha-256");
        val.Should().Be("abc123=");
    }

    [Fact]
    public void TryParseContentDigestHeader_ShouldPickFirstValidPair_FromMultiple()
    {
        DigestParser.TryParseContentDigestHeader("sha-256=:abc=:, sha-512=:def=:", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("sha-256");
        val.Should().Be("abc=");
    }

    [Fact]
    public void TryParseContentDigestHeader_ShouldFallbackToLegacyFormat()
    {
        DigestParser.TryParseContentDigestHeader("SHA-256=abc", out var alg, out var val).Should().BeTrue();
        alg.Should().Be("SHA-256");
        val.Should().Be("abc");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noequals")]
    [InlineData("=noval")]
    [InlineData("alg=")]
    public void TryParseContentDigestHeader_ShouldReturnFalse_ForInvalid(string? input)
    {
        DigestParser.TryParseContentDigestHeader(input!, out _, out _).Should().BeFalse();
    }
}