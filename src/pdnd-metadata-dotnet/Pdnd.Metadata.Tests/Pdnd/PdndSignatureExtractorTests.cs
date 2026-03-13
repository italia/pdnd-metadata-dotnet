// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndSignatureExtractorTests
{
    [Fact]
    public void Extract_ShouldAddHeaderAndPayloadFields_WhenPresent()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts(
            HeaderJson: "{\"alg\":\"RS256\",\"kid\":\"k1\",\"typ\":\"JWT\"}",
            PayloadJson: "{\"iss\":\"i\",\"sub\":\"s\",\"jti\":\"j\",\"aud\":\"api\",\"iat\":1700000000,\"exp\":1700003600,\"signed_headers\":\"digest content-type\"}",
            SignatureBase64Url: "sig");

        PdndSignatureExtractor.Extract(md, token);

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureAlg).Should().Be("RS256");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureKid).Should().Be("k1");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureTyp).Should().Be("JWT");

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureIss).Should().Be("i");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSub).Should().Be("s");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureJti).Should().Be("j");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureAud).Should().Be("api");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureIat).Should().Be("1700000000");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureExp).Should().Be("1700003600");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().Be("digest content-type");
    }

    [Fact]
    public void Extract_ShouldHandleAudienceArray()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts(
            HeaderJson: "{\"alg\":\"RS256\"}",
            PayloadJson: "{\"aud\":[\"api1\",\"api2\"]}",
            SignatureBase64Url: null);

        PdndSignatureExtractor.Extract(md, token);

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureAud).Should().Be("api1, api2");
    }

    [Fact]
    public void Extract_ShouldNotThrow_OnInvalidJson()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts("{bad", "{bad", null);

        var act = () => PdndSignatureExtractor.Extract(md, token);
        act.Should().NotThrow();
    }
}
