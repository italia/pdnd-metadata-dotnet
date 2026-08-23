// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction.Pdnd;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndAuthorizationTests
{
    [Fact]
    public void TryGetBearerToken_ShouldExtractToken_CaseInsensitive()
    {
        PdndAuthorization.TryGetBearerToken("Bearer abc", out var token).Should().BeTrue();
        token.Should().Be("abc");

        PdndAuthorization.TryGetBearerToken("bearer xyz", out token).Should().BeTrue();
        token.Should().Be("xyz");
    }

    [Fact]
    public void TryGetBearerToken_ShouldTrimToken()
    {
        PdndAuthorization.TryGetBearerToken("Bearer   abc   ", out var token).Should().BeTrue();
        token.Should().Be("abc");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    [InlineData("Bearer   ")]
    public void TryGetBearerToken_ShouldReturnFalse_WhenInvalid(string? header)
    {
        PdndAuthorization.TryGetBearerToken(header, out _).Should().BeFalse();
    }
}