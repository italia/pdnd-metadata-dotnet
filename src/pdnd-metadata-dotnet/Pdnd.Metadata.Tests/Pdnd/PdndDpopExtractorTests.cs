// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndDpopExtractorTests
{
    [Fact]
    public void Extract_ShouldAddHeaderAndPayloadFields_WhenPresent()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts(
            HeaderJson: "{\"alg\":\"ES256\",\"kid\":\"kid\",\"typ\":\"dpop+jwt\"}",
            PayloadJson: "{\"htm\":\"GET\",\"htu\":\"https://api/x\",\"jti\":\"j\",\"iat\":1700000000,\"exp\":1700003600,\"ath\":\"fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo\",\"nonce\":\"eyJ7S_zG.eyJH0-Z.HX4w-7v\"}",
            SignatureBase64Url: "sig");

        PdndDpopExtractor.Extract(md, token);

        md.GetFirstValue(PdndMetadataKeys.PdndDpopAlg).Should().Be("ES256");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopKid).Should().Be("kid");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopTyp).Should().Be("dpop+jwt");

        md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm).Should().Be("GET");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopHtu).Should().Be("https://api/x");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopJti).Should().Be("j");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopIat).Should().Be("1700000000");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopExp).Should().Be("1700003600");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopAth).Should().Be("fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo");
        md.GetFirstValue(PdndMetadataKeys.PdndDpopNonce).Should().Be("eyJ7S_zG.eyJH0-Z.HX4w-7v");
    }

    [Fact]
    public void Extract_ShouldNotThrow_OnInvalidJson()
    {
        var md = new PdndCallerMetadata();
        var token = new JwtParts("{bad", "{bad", null);

        var act = () => PdndDpopExtractor.Extract(md, token);
        act.Should().NotThrow();
    }
}