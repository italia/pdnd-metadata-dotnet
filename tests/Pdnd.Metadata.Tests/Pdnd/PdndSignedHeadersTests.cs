// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Pdnd;

/// <summary>
/// Covers the ModI <c>signed_headers</c> claim, which is an array rather than a scalar.
/// Before these tests existed the claim was read with a string-only reader, so the canonical
/// key was silently absent on every conformant request.
/// </summary>
public sealed class PdndSignedHeadersTests
{
    private static PdndCallerMetadata Extract(string signatureToken, PdndMetadataOptions? options = null)
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            Scheme = "https",
            Host = "erogatore.example.it",
            Path = "/v1/resource",
            Headers = new List<PdndRequestHeader>
            {
                new("Agid-JWT-Signature", new[] { signatureToken })
            }
        };

        return new DefaultPdndMetadataExtractor().Extract(context, options ?? new PdndMetadataOptions());
    }

    [Fact]
    public void Extract_WithModiArrayOfObjects_PopulatesSignedHeaderNames()
    {
        var token = Base64UrlTestHelper.Jwt(
            """{"alg":"RS256","kid":"k1"}""",
            """{"iss":"fruitore","signed_headers":[{"digest":"SHA-256=abc="},{"content-type":"application/json"}]}""");

        var metadata = Extract(token);

        metadata.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders)
            .Should().Be("digest, content-type");
    }

    [Fact]
    public void Extract_WithModiArrayOfObjects_PopulatesEachSignedHeaderValue()
    {
        var token = Base64UrlTestHelper.Jwt(
            """{"alg":"RS256"}""",
            """{"signed_headers":[{"digest":"SHA-256=abc="},{"content-type":"application/json"}]}""");

        var metadata = Extract(token);

        metadata.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "digest")
            .Should().Be("SHA-256=abc=");
        metadata.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "content-type")
            .Should().Be("application/json");
    }

    [Fact]
    public void Extract_WithArrayOfNames_NormalizesToLowercase()
    {
        var token = Base64UrlTestHelper.Jwt(
            """{"alg":"RS256"}""",
            """{"signed_headers":["Digest","Content-Type"]}""");

        Extract(token).GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders)
            .Should().Be("digest, content-type");
    }

    [Fact]
    public void Extract_WithCommaSeparatedString_IsAccepted()
    {
        var token = Base64UrlTestHelper.Jwt(
            """{"alg":"RS256"}""",
            """{"signed_headers":"Digest, Content-Type"}""");

        Extract(token).GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders)
            .Should().Be("digest, content-type");
    }

    [Fact]
    public void Extract_WithoutClaim_ProducesNoKey()
    {
        var token = Base64UrlTestHelper.Jwt("""{"alg":"RS256"}""", """{"iss":"fruitore"}""");

        Extract(token).GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().BeNull();
    }

    [Fact]
    public void Extract_WithEmptyArray_ProducesNoKey()
    {
        var token = Base64UrlTestHelper.Jwt("""{"alg":"RS256"}""", """{"signed_headers":[]}""");

        Extract(token).GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().BeNull();
    }

    [Fact]
    public void Extract_WithMalformedClaim_DoesNotThrowAndProducesNoKey()
    {
        var token = Base64UrlTestHelper.Jwt("""{"alg":"RS256"}""", """{"signed_headers":42}""");

        Extract(token).GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().BeNull();
    }
}
