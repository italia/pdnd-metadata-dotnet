// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Extraction;

public class DefaultPdndMetadataExtractorTests
{
    [Fact]
    public void Extract_ShouldAddBasicHttpAndConnectionFields()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Scheme = "https",
            Host = "example.test",
            Path = "/x",
            QueryString = "?a=1",
            RemoteIpAddress = IPAddress.Parse("10.0.0.1"),
            RemotePort = 12345,
            LocalIpAddress = IPAddress.Parse("10.0.0.2"),
            LocalPort = 443,
            Headers = Array.Empty<PdndRequestHeader>()
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.HttpMethod).Should().Be("GET");
        md.GetFirstValue(PdndMetadataKeys.HttpScheme).Should().Be("https");
        md.GetFirstValue(PdndMetadataKeys.HttpHost).Should().Be("example.test");
        md.GetFirstValue(PdndMetadataKeys.HttpPath).Should().Be("/x");
        md.GetFirstValue(PdndMetadataKeys.HttpQuery).Should().Be("?a=1");

        md.GetFirstValue(PdndMetadataKeys.NetRemoteIp).Should().Be("10.0.0.1");
        md.GetFirstValue(PdndMetadataKeys.NetRemotePort).Should().Be("12345");
        md.GetFirstValue(PdndMetadataKeys.NetLocalIp).Should().Be("10.0.0.2");
        md.GetFirstValue(PdndMetadataKeys.NetLocalPort).Should().Be("443");
    }

    [Fact]
    public void Extract_ShouldPromoteTracingHeaders_WhenEnabled()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            TracingHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
                ["tracestate"] = "k=v",
                ["baggage"] = "a=b",
                ["x-correlation-id"] = "corr-1"
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions { PromoteTracingHeaders = true });

        md.GetFirstValue(PdndMetadataKeys.TraceParent).Should().Be("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        md.GetFirstValue(PdndMetadataKeys.TraceState).Should().Be("k=v");
        md.GetFirstValue(PdndMetadataKeys.TraceBaggage).Should().Be("a=b");
        md.GetFirstValue(PdndMetadataKeys.CorrelationId).Should().Be("corr-1");
    }

    [Fact]
    public void Extract_ShouldCaptureHeaders_ButRespectDenyList()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader("User-Agent", new[] { "ua" }),
                new PdndRequestHeader("Authorization", new[] { "Bearer x.y.z" }),
                new PdndRequestHeader("Cookie", new[] { "a=b" }),
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions { CaptureAllHeaders = true });

        md.Items.Keys.Should().Contain(k => k == "http.header.user-agent");
        md.Items.Keys.Should().NotContain(k => k.Contains("authorization", StringComparison.OrdinalIgnoreCase));
        md.Items.Keys.Should().NotContain(k => k.Contains("cookie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_ShouldParsePdndVoucher_FromAuthorizationBearer_WhenEnabled()
    {
        var jwt = Base64UrlTestHelper.Jwt(
            headerJson: "{\"alg\":\"none\"}",
            payloadJson: "{\"iss\":\"issuer\",\"purposeId\":\"p1\",\"clientId\":\"c1\"}");

        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader(PdndHeaderNames.Authorization, new[] { "Bearer " + jwt })
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions
        {
            ParsePdndVoucherFromAuthorizationBearer = true,
            CaptureAllHeaders = true // still must not store Authorization raw
        });

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss).Should().Be("issuer");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("p1");
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId).Should().Be("c1");

        md.Items.Keys.Should().NotContain(k => k.Contains("authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_ShouldParseTrackingEvidence_AndNotCaptureRawHeader_ByDefault()
    {
        var te = Base64UrlTestHelper.Jwt(
            headerJson: "{\"alg\":\"RS256\",\"kid\":\"k\"}",
            payloadJson: "{\"iss\":\"i\",\"sub\":\"s\",\"jti\":\"j\"}",
            signature: "sig");

        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader(PdndHeaderNames.AgidJwtTrackingEvidence, new[] { te })
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions
        {
            ParsePdndTrackingEvidence = true,
            CaptureAllHeaders = true,
            CaptureRawTrackingEvidenceHeader = false
        });

        md.GetFirstValue(PdndMetadataKeys.PdndTrackingAlg).Should().Be("RS256");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingKid).Should().Be("k");
        md.GetFirstValue(PdndMetadataKeys.PdndTrackingIss).Should().Be("i");

        // Ensure the raw signed blob is not captured
        md.Items.Keys.Should().NotContain(k => k.Contains("http.header.", StringComparison.OrdinalIgnoreCase)
                                              && k.Contains("agid-jwt-tracking-evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_ShouldNormalizeForwardedHeaders_WhenEnabled()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader("Forwarded", new[] { "for=1.2.3.4;proto=https, for=5.6.7.8" })
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions { NormalizeForwardedHeaders = true });

        md.GetFirstValue(PdndMetadataKeys.NetForwardedFor).Should().Be("1.2.3.4, 5.6.7.8");
    }

    [Fact]
    public void Extract_ShouldAddClaims_WithPrefix()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Claims = new[]
            {
                new Claim("sub", "user1"),
                new Claim("role", "admin"),
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions());

        md.Items.Keys.Should().Contain("claim.sub");
        md.Items.Keys.Should().Contain("claim.role");
    }

    [Fact]
    public void Extract_ShouldTruncateValues_WhenExceedingMaxValueLength()
    {
        var longValue = new string('x', 500);
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader("User-Agent", new[] { longValue })
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions
        {
            CaptureAllHeaders = true,
            MaxValueLength = 10
        });

        md.GetFirstValue("http.header.user-agent")!.Length.Should().Be(10);
    }

    [Fact]
    public void Extract_ShouldBeFailSoft_WhenPdndTokensAreInvalid()
    {
        var ctx = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader(PdndHeaderNames.Authorization, new[] { "Bearer not-a-jwt" }),
                new PdndRequestHeader(PdndHeaderNames.DPoP, new[] { "not-a-jwt" }),
                new PdndRequestHeader(PdndHeaderNames.AgidJwtTrackingEvidence, new[] { "not-a-jwt" }),
                new PdndRequestHeader(PdndHeaderNames.Digest, new[] { "bad" }),
            }
        };

        var act = () => new DefaultPdndMetadataExtractor().Extract(ctx, new PdndMetadataOptions());
        act.Should().NotThrow();
    }
}