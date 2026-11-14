// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Extraction;

/// <summary>
/// Pins extraction against the token shapes in the current PDND operating manual
/// (developer.pagopa.it) and the AgID AUDIT_REST_01/02 audit claims.
/// </summary>
public sealed class PdndCurrentTokenShapeTests
{
    private const string VoucherPayload =
        "{\"iss\":\"interop.pagopa.it\",\"nbf\":1747408537,\"iat\":1747408537,\"exp\":1747409537," +
        "\"jti\":\"12297ac1\",\"aud\":\"https://eservice.pa.it/api/v1\",\"sub\":\"client-1\"," +
        "\"client_id\":\"client-1\",\"purposeId\":\"purpose-1\",\"producerId\":\"producer-1\"," +
        "\"consumerId\":\"consumer-1\",\"eserviceId\":\"eservice-1\",\"descriptorId\":\"descriptor-1\"," +
        "\"cnf\":{\"jkt\":\"L5TP6x6ved3p_jmIAtCiHMcNJeRrGWAusNnQkTTrnLY\"}," +
        "\"digest\":{\"alg\":\"SHA256\",\"value\":\"5db26201\"}}";

    private static PdndRequestContext Context(params PdndRequestHeader[] headers) => new()
    {
        Method = "GET",
        Path = "/x",
        Headers = headers
    };

    [Theory]
    [InlineData("Bearer")]
    [InlineData("DPoP")]
    public void Extract_ReadsThePartyAndEserviceIdentifiers_FromACurrentVoucher(string scheme)
    {
        var voucher = Base64UrlTestHelper.Jwt("{\"typ\":\"at+jwt\",\"alg\":\"RS256\",\"kid\":\"k\"}", VoucherPayload, "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader("Authorization", new[] { scheme + " " + voucher })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().Be("consumer-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherProducerId).Should().Be("producer-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherEserviceId).Should().Be("eservice-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDescriptorId).Should().Be("descriptor-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherCnfJkt).Should().Be("L5TP6x6ved3p_jmIAtCiHMcNJeRrGWAusNnQkTTrnLY");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientIdUnderscore).Should().Be("client-1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherDigestAlg).Should().Be("SHA256");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId).Should().BeNull();
    }

    [Fact]
    public void Extract_IgnoresANonObjectCnfClaim()
    {
        var voucher = Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"cnf\":\"not-an-object\",\"consumerId\":\"c\"}", "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader("Authorization", new[] { "Bearer " + voucher })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherCnfJkt).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().Be("c");
    }

    [Theory]
    [InlineData("Agid-JWT-TrackingEvidence")]
    [InlineData("AgID-JWT-TrackingEvidence")]
    [InlineData("Agid-JWT-Tracking-Evidence")]
    public void Extract_ReadsTheAuditClaims_FromTheTrackingEvidence(string headerName)
    {
        var evidence = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\",\"kid\":\"kid-fruitore\",\"typ\":\"JWT\"}",
            "{\"iss\":\"client-1\",\"aud\":\"https://eservice.pa.it/api/v1\",\"purposeId\":\"purpose-1\"," +
            "\"dnonce\":\"6255429286164\",\"jti\":\"te-1\",\"iat\":1747408537,\"exp\":1747408837," +
            "\"userID\":\"user-001\",\"userLocation\":\"postazione-042\",\"LoA\":\"SPID_L2\"}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            Context(new PdndRequestHeader(headerName, new[] { evidence })),
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndTrackingUserId).Should().Be("user-001");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingUserLocation).Should().Be("postazione-042");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingLoA).Should().Be("SPID_L2");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingPurposeId).Should().Be("purpose-1");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingDnonce).Should().Be("6255429286164");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingJti).Should().Be("te-1");
    }
}
