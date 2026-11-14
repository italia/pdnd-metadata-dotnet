// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Extraction;

/// <summary>
/// Pins the behaviour of the DPoP-bound voucher, the multi-line forwarded chain, the
/// segment-aware path exclusions, and the value bound on token claims.
/// </summary>
public sealed class ReviewFixesTests
{
    private static PdndRequestContext Context(params PdndRequestHeader[] headers) => new()
    {
        Method = "GET",
        Path = "/x",
        Headers = headers
    };

    // --- DPoP-bound voucher -------------------------------------------------------------

    [Theory]
    [InlineData("DPoP abc", "abc")]
    [InlineData("dpop   abc  ", "abc")]
    [InlineData("Bearer abc", "abc")]
    public void TryGetVoucherToken_AcceptsBearerAndDpopSchemes(string header, string expected)
    {
        PdndAuthorization.TryGetVoucherToken(header, out var token).Should().BeTrue();
        token.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic abc")]
    [InlineData("DPoP")]
    [InlineData("DPoP   ")]
    [InlineData("DPoPabc")]
    [InlineData("Bearerabc")]
    public void TryGetVoucherToken_RejectsOtherSchemes(string? header)
    {
        PdndAuthorization.TryGetVoucherToken(header, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetBearerToken_StillRejectsTheDpopScheme()
    {
        PdndAuthorization.TryGetBearerToken("DPoP abc", out _).Should().BeFalse();
    }

    [Fact]
    public void Extract_ReadsTheVoucher_FromADpopBoundAuthorizationHeader()
    {
        var voucher = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\",\"typ\":\"at+jwt\"}",
            "{\"purposeId\":\"purpose-dpop\",\"organizationId\":\"org-dpop\",\"cnf\":{\"jkt\":\"x\"}}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader("Authorization", new[] { "DPoP " + voucher })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("purpose-dpop");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId).Should().Be("org-dpop");
        md.GetFirstValue("http.header.authorization").Should().BeNull();
    }

    // --- Forwarded chain across repeated header lines ----------------------------------

    [Fact]
    public void Extract_JoinsEveryXForwardedForValue_InArrivalOrder()
    {
        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader("X-Forwarded-For", new[] { "1.1.1.1, 2.2.2.2", "3.3.3.3" })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.NetForwardedFor).Should().Be("1.1.1.1, 2.2.2.2, 3.3.3.3");
    }

    [Fact]
    public void Extract_JoinsForwardedValues_AcrossSeparateHeaderEntries()
    {
        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(
                new PdndRequestHeader("Forwarded", new[] { "for=1.1.1.1" }),
                new PdndRequestHeader("forwarded", new[] { "for=\"[2001:db8::1]\";proto=https" })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.NetForwardedFor).Should().Be("1.1.1.1, 2001:db8::1");
    }

    [Fact]
    public void Extract_TokenHeaders_StillUseTheFirstValueOnly()
    {
        var first = Base64UrlTestHelper.Jwt("{\"alg\":\"ES256\"}", "{\"htm\":\"GET\"}", "s");
        var second = Base64UrlTestHelper.Jwt("{\"alg\":\"ES256\"}", "{\"htm\":\"POST\"}", "s");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader("DPoP", new[] { first, second })),
            new PdndMetadataOptions());

        md.GetValues(PdndMetadataKeys.PdndDpopHtm).Should().Equal("GET");
    }

    // --- Segment-aware path exclusions ------------------------------------------------

    [Theory]
    [InlineData("/health", "/health", true)]
    [InlineData("/health", "/HEALTH", true)]
    [InlineData("/health", "/health/ready", true)]
    [InlineData("/health/", "/health", true)]
    [InlineData("/health/", "/health/live", true)]
    [InlineData("/health", "/healthcare", false)]
    [InlineData("/health", "/health-records", false)]
    [InlineData("/health", "/api/health", false)]
    [InlineData("/", "/anything", true)]
    public void IsPathExcluded_MatchesWholeSegments(string exclusion, string path, bool expected)
    {
        var options = new PdndMetadataOptions();
        options.PathExclusions.Add(exclusion);

        options.IsPathExcluded(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsPathExcluded_IgnoresEmptyEntries_RatherThanExcludingEverything(string? exclusion)
    {
        var options = new PdndMetadataOptions();
        options.PathExclusions.Add(exclusion!);

        options.IsPathExcluded("/api/resource").Should().BeFalse();
    }

    // --- MaxValueLength applies to token claims -----------------------------------------

    [Fact]
    public void Extract_TruncatesTokenClaims_ToMaxValueLength()
    {
        var longValue = new string('p', 500);
        var voucher = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"purposeId\":\"" + longValue + "\"}",
            "sig");
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"signed_headers\":[{\"digest\":\"SHA-256=" + longValue + "\"}]}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(
                new PdndRequestHeader("Authorization", new[] { "Bearer " + voucher }),
                new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }),
                new PdndRequestHeader("Digest", new[] { "SHA-256=" + longValue })),
            new PdndMetadataOptions { MaxValueLength = 32 });

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().HaveLength(32);
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "digest").Should().HaveLength(32);
        md.GetFirstValue(PdndMetadataKeys.PdndDigestValue).Should().HaveLength(32);
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherAlg).Should().Be("RS256");
    }
}
