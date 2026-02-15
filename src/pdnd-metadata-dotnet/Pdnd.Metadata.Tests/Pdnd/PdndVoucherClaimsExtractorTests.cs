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
            HeaderJson: "{\"alg\":\"none\"}",
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
                           "clientId":"c1"
                         }
                         """,
            SignatureBase64Url: null);

        PdndVoucherClaimsExtractor.Extract(md, jwt);

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss).Should().Be("issuer");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherSub).Should().Be("subject");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherAud).Should().Be("a, b");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherJti).Should().Be("id");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherIat).Should().Be("1700000000");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherNbf).Should().Be("1700000001");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherExp).Should().Be("1700000002");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("p1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId).Should().Be("c1");
    }

    [Fact]
    public void Extract_ShouldNotThrow_OnInvalidJson()
    {
        var md = new PdndCallerMetadata();
        var jwt = new JwtParts("{}", "{ not-json", null);

        var act = () => PdndVoucherClaimsExtractor.Extract(md, jwt);
        act.Should().NotThrow();
        md.Items.Should().BeEmpty();
    }
}