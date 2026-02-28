// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Jwt;

public class JwtDecoderTests
{
    [Fact]
    public void TryDecode_ShouldReturnFalse_WhenTokenIsNullOrWhitespace()
    {
        JwtDecoder.TryDecode(null!, out _).Should().BeFalse();
        JwtDecoder.TryDecode("", out _).Should().BeFalse();
        JwtDecoder.TryDecode("   ", out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldReturnFalse_WhenLessThanTwoSegments()
    {
        JwtDecoder.TryDecode("abc", out _).Should().BeFalse();
        JwtDecoder.TryDecode("a.b", out _).Should().BeFalse(); // not valid base64url -> decode fails
    }

    [Fact]
    public void TryDecode_ShouldDecodeHeaderAndPayload_WhenTwoSegments_WithoutSignature()
    {
        var token = Base64UrlTestHelper.Jwt(
            headerJson: "{\"alg\":\"none\"}",
            payloadJson: "{\"iss\":\"x\"}");

        JwtDecoder.TryDecode(token, out var parts).Should().BeTrue();
        parts.HeaderJson.Should().Contain("\"alg\"");
        parts.PayloadJson.Should().Contain("\"iss\"");
        parts.SignatureBase64Url.Should().BeNull();
    }

    [Fact]
    public void TryDecode_ShouldKeepSignature_WhenThreeSegments()
    {
        var token = Base64UrlTestHelper.Jwt(
            headerJson: "{\"alg\":\"RS256\"}",
            payloadJson: "{\"sub\":\"y\"}",
            signature: "sigPart");

        JwtDecoder.TryDecode(token, out var parts).Should().BeTrue();
        parts.SignatureBase64Url.Should().Be("sigPart");
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".abc.")]
    [InlineData("abc..def")]
    public void TryDecode_ShouldReturnFalse_ForClearlyMalformedToken(string token)
    {
        JwtDecoder.TryDecode(token, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldReturnFalse_WhenSegmentsBetweenDotsAreEmpty()
    {
        // "header..payload" has an empty second segment — invalid JWT structure
        var header = Base64UrlTestHelper.EncodeUtf8("{\"alg\":\"none\"}");
        var payload = Base64UrlTestHelper.EncodeUtf8("{\"iss\":\"x\"}");
        var token = $"{header}..{payload}";

        JwtDecoder.TryDecode(token, out _).Should().BeFalse();
    }
}