// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndTrackingEvidenceExtractorTests
{
    [Fact]
    public void Extract_ShouldAddHeaderAndPayloadFields_WhenPresent()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts(
            HeaderJson: "{\"alg\":\"RS256\",\"kid\":\"k1\",\"typ\":\"JWT\"}",
            PayloadJson: "{\"iss\":\"i\",\"sub\":\"s\",\"jti\":\"j\"}",
            SignatureBase64Url: "sig");

        PdndTrackingEvidenceExtractor.Extract(md, token);

        md.GetFirstValue(PdndMetadataKeys.PdndTrackingAlg).Should().Be("RS256");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingKid).Should().Be("k1");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingTyp).Should().Be("JWT");

        md.GetFirstValue(PdndMetadataKeys.PdndTrackingIss).Should().Be("i");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingSub).Should().Be("s");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingJti).Should().Be("j");
    }

    [Fact]
    public void Extract_ShouldNotThrow_OnInvalidJson()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts("{bad", "{bad", null);

        var act = () => PdndTrackingEvidenceExtractor.Extract(md, token);
        act.Should().NotThrow();
    }
}
