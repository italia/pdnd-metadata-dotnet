// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndVoucherClaimsExtractorTests
{
    [Fact]
    public void Extract_ShouldAddKnownVoucherClaims_WhenPresent()
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts(
            HeaderJson: "{\"alg\":\"RS256\",\"kid\":\"key-1\",\"typ\":\"at+jwt\"}",
            PayloadJson: """
                         {
                           "iss":"issuer",
                           "sub":"subject",
                           "aud":["a","b"],
                           "jti":"id",
                           "iat":1700000000,
                           "nbf":1700000001,
                           "exp":1700000002,
                           "purposeId":"p1",
                           "clientId":"c1",
                           "client_id":"c2",
                           "organizationId":"org-1",
                           "dnonce":"nonce-abc"
                         }
                         """,
            SignatureBase64Url: null);

        PdndVoucherClaimsExtractor.Extract(md, jwt);

        // JOSE header
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherAlg).Should().Be("RS256");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherKid).Should().Be("key-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherTyp).Should().Be("at+jwt");

        // Standard JWT payload
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss).Should().Be("issuer");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherSub).Should().Be("subject");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherAud).Should().Be("a, b");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherJti).Should().Be("id");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherIat).Should().Be("1700000000");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherNbf).Should().Be("1700000001");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherExp).Should().Be("1700000002");

        // PDND-specific payload
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("p1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId).Should().Be("c1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientIdUnderscore).Should().Be("c2");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId).Should().Be("org-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDnonce).Should().Be("nonce-abc");
    }

    [Fact]
    public void Extract_ShouldAddDigestClaim_WhenPresent()
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts(
            HeaderJson: "{\"alg\":\"RS256\"}",
            PayloadJson: "{\"purposeId\":\"p1\",\"digest\":{\"alg\":\"SHA256\",\"value\":\"abc123\"}}",
            SignatureBase64Url: null);

        PdndVoucherClaimsExtractor.Extract(md, jwt);

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestAlg).Should().Be("SHA256");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestValue).Should().Be("abc123");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("p1");
    }

    [Theory]
    [InlineData("{\"digest\":\"SHA256=abc\"}")]
    [InlineData("{\"digest\":[\"SHA256\",\"abc\"]}")]
    [InlineData("{\"digest\":null}")]
    [InlineData("{\"digest\":{}}")]
    [InlineData("{\"digest\":{\"alg\":\"\",\"value\":\"\"}}")]
    public void Extract_ShouldIgnoreDigestClaim_WhenMalformed(string payload)
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts("{}", payload, null);

        var act = () => PdndVoucherClaimsExtractor.Extract(md, jwt);

        act.Should().NotThrow();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestAlg).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestValue).Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldAddPartialDigest_WhenOnlyAlgIsPresent()
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts("{}", "{\"digest\":{\"alg\":\"SHA256\"}}", null);

        PdndVoucherClaimsExtractor.Extract(md, jwt);

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestAlg).Should().Be("SHA256");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestValue).Should().BeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"purposeId\":\"p1\"}]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    public void Extract_ShouldNotThrow_WhenPayloadRootIsNotObject(string payload)
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts("{\"alg\":\"RS256\"}", payload, null);

        var act = () => PdndVoucherClaimsExtractor.Extract(md, jwt);

        act.Should().NotThrow();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherAlg).Should().Be("RS256");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldNotThrow_OnInvalidJson()
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts("{}", "{ not-json", null);

        var act = () => PdndVoucherClaimsExtractor.Extract(md, jwt);
        act.Should().NotThrow();
        md.Snapshot().Should().BeEmpty();
    }
}
