// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Extraction;

/// <summary>
/// Pins what each extraction switch does when turned off, and the bounds that are otherwise only
/// tested through their setters.
/// </summary>
public sealed class ExtractionOptionsBehaviourTests
{
    private static readonly string Voucher =
        Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"purposeId\":\"p\"}", "sig");

    private static readonly string Proof =
        Base64UrlTestHelper.Jwt("{\"typ\":\"dpop+jwt\"}", "{\"htm\":\"GET\"}", "sig");

    private static readonly string Evidence =
        Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"jti\":\"te\"}", "sig");

    private static readonly string Signature =
        Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"jti\":\"sig\"}", "sig");

    private static PdndRequestContext PdndRequest() => new()
    {
        Method = "POST",
        Path = "/x",
        Headers = new[]
        {
            new PdndRequestHeader("Authorization", new[] { "Bearer " + Voucher }),
            new PdndRequestHeader("DPoP", new[] { Proof }),
            new PdndRequestHeader("Agid-JWT-TrackingEvidence", new[] { Evidence }),
            new PdndRequestHeader("Agid-JWT-Signature", new[] { Signature }),
            new PdndRequestHeader("Digest", new[] { "SHA-256=abc=" }),
            new PdndRequestHeader("Content-Digest", new[] { "sha-256=:abc=:" })
        }
    };

    private static readonly string[] ControlledKeys =
    [
        PdndMetadataKeys.PdndVoucherPurposeId,
        PdndMetadataKeys.PdndDpopHtm,
        PdndMetadataKeys.PdndTrackingJti,
        PdndMetadataKeys.PdndSignatureJti,
        PdndMetadataKeys.PdndDigestAlg,
        PdndMetadataKeys.PdndContentDigestAlg
    ];

    public static TheoryData<string, string> SwitchesAndTheKeyTheyControl() => new()
    {
        { nameof(PdndMetadataOptions.ParsePdndVoucherFromAuthorizationBearer), PdndMetadataKeys.PdndVoucherPurposeId },
        { nameof(PdndMetadataOptions.ParseDpopHeader), PdndMetadataKeys.PdndDpopHtm },
        { nameof(PdndMetadataOptions.ParsePdndTrackingEvidence), PdndMetadataKeys.PdndTrackingJti },
        { nameof(PdndMetadataOptions.ParseAgidJwtSignature), PdndMetadataKeys.PdndSignatureJti },
        { nameof(PdndMetadataOptions.ParseDigestHeader), PdndMetadataKeys.PdndDigestAlg },
        { nameof(PdndMetadataOptions.ParseContentDigestHeader), PdndMetadataKeys.PdndContentDigestAlg }
    };

    [Theory]
    [MemberData(nameof(SwitchesAndTheKeyTheyControl))]
    public void ParseSwitch_WhenOff_SuppressesOnlyItsOwnKeys(string optionName, string key)
    {
        var enabled = new DefaultPdndMetadataExtractor().Extract(PdndRequest(), new PdndMetadataOptions());
        enabled.GetFirstValue(key).Should().NotBeNull("the switch is on by default");

        var options = new PdndMetadataOptions();
        typeof(PdndMetadataOptions).GetProperty(optionName)!.SetValue(options, false);

        var disabled = new DefaultPdndMetadataExtractor().Extract(PdndRequest(), options);

        disabled.GetFirstValue(key).Should().BeNull();

        // Every other token is still decoded.
        foreach (var other in ControlledKeys.Where(k => k != key))
            disabled.GetFirstValue(other).Should().NotBeNull($"turning off {optionName} must not affect {other}");
    }

    public static TheoryData<string, string> RawCaptureSwitches() => new()
    {
        { nameof(PdndMetadataOptions.CaptureRawDpopHeader), "dpop" },
        { nameof(PdndMetadataOptions.CaptureRawTrackingEvidenceHeader), "agid-jwt-trackingevidence" },
        { nameof(PdndMetadataOptions.CaptureRawSignatureHeader), "agid-jwt-signature" }
    };

    [Theory]
    [MemberData(nameof(RawCaptureSwitches))]
    public void CaptureRawSwitch_WhenOn_RetainsTheRawBlob(string optionName, string headerKey)
    {
        var key = PdndMetadataKeys.HeaderPrefix + headerKey;

        new DefaultPdndMetadataExtractor().Extract(PdndRequest(), new PdndMetadataOptions())
            .GetFirstValue(key).Should().BeNull("signed blobs are not retained by default");

        var options = new PdndMetadataOptions();
        typeof(PdndMetadataOptions).GetProperty(optionName)!.SetValue(options, true);

        new DefaultPdndMetadataExtractor().Extract(PdndRequest(), options)
            .GetFirstValue(key).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CaptureRawDigestHeader_IsOnByDefault_AndCanBeTurnedOff()
    {
        new DefaultPdndMetadataExtractor().Extract(PdndRequest(), new PdndMetadataOptions())
            .GetFirstValue(PdndMetadataKeys.HeaderPrefix + "digest").Should().Be("SHA-256=abc=");

        var md = new DefaultPdndMetadataExtractor().Extract(
            PdndRequest(), new PdndMetadataOptions { CaptureRawDigestHeader = false });

        md.GetFirstValue(PdndMetadataKeys.HeaderPrefix + "digest").Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.HeaderPrefix + "content-digest").Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndDigestAlg).Should().Be("SHA-256", "parsing is independent of raw capture");
    }

    [Fact]
    public void Authorization_IsNeverCaptured_EvenWhenAllowListedAndCaptureAllIsOn()
    {
        var options = new PdndMetadataOptions { CaptureAllHeaders = true };
        options.HeaderAllowList.Add("Authorization");

        var md = new DefaultPdndMetadataExtractor().Extract(PdndRequest(), options);

        md.GetFirstValue(PdndMetadataKeys.HeaderPrefix + "authorization").Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().Be("p");
    }

    [Fact]
    public void PromoteTracingHeaders_WhenOff_ProducesNoTracingOrCorrelationKeys()
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            TracingHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                ["x-correlation-id"] = "corr-1",
                ["x-request-id"] = "req-1"
            }
        };

        var on = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions());
        on.GetFirstValue(PdndMetadataKeys.CorrelationId).Should().Be("corr-1");
        on.GetFirstValue(PdndMetadataKeys.RequestId).Should().Be("req-1");

        var off = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { PromoteTracingHeaders = false });
        off.GetFirstValue(PdndMetadataKeys.TraceParent).Should().BeNull();
        off.GetFirstValue(PdndMetadataKeys.CorrelationId).Should().BeNull();
        off.GetFirstValue(PdndMetadataKeys.RequestId).Should().BeNull();
    }

    [Fact]
    public void NormalizeForwardedHeaders_WhenOff_ProducesNoForwardedChain()
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[] { new PdndRequestHeader("X-Forwarded-For", new[] { "1.1.1.1" }) }
        };

        new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { NormalizeForwardedHeaders = false })
            .GetFirstValue(PdndMetadataKeys.NetForwardedFor).Should().BeNull();
    }

    [Fact]
    public void Forwarded_WithoutForParameters_FallsBackToXForwardedFor()
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader("Forwarded", new[] { "proto=https;host=example.it" }),
                new PdndRequestHeader("X-Forwarded-For", new[] { "2.2.2.2" })
            }
        };

        new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions())
            .GetFirstValue(PdndMetadataKeys.NetForwardedFor).Should().Be("2.2.2.2");
    }

    [Fact]
    public void MaxHeaderValuesPerName_BoundsTheValuesCapturedForOneHeader()
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            Headers = new[] { new PdndRequestHeader("User-Agent", new[] { "a", "b", "c", "d" }) }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { MaxHeaderValuesPerName = 2 });

        md.GetValues(PdndMetadataKeys.HeaderPrefix + "user-agent").Should().Equal("a", "b");
    }

    [Fact]
    public void SecurityHints_ArePrefixedAndTruncated()
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            SecurityHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mtls.client_certificate_thumbprint"] = new string('A', 40),
                ["empty"] = " "
            }
        };

        var md = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { MaxValueLength = 8 });

        md.GetFirstValue("security.mtls.client_certificate_thumbprint").Should().Be("AAAAAAAA");
        md.GetFirstValue("security.empty").Should().BeNull();
        md.Snapshot()["security.mtls.client_certificate_thumbprint"].Single().Source.Should().Be(PdndMetadataSource.Tls);
    }

    [Fact]
    public void ClaimValuesThatAreNotScalars_ProduceNoKey()
    {
        var voucher = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"purposeId\":{\"nested\":1},\"consumerId\":true,\"eserviceId\":null,\"producerId\":[\"x\"],\"jti\":\"j\"}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            new PdndRequestContext { Method = "GET", Headers = new[] { new PdndRequestHeader("Authorization", new[] { "Bearer " + voucher }) } },
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherEserviceId).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherProducerId).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndVoucherJti).Should().Be("j");
    }

    [Fact]
    public void SignedHeaderValuesThatAreNotStrings_AreKeptAsRawJson()
    {
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"signed_headers\":[{\"content-length\":42}]}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(
            new PdndRequestContext { Method = "GET", Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) } },
            new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "content-length").Should().Be("42");
    }

    [Fact]
    public void Extract_RejectsNullArguments()
    {
        var extractor = new DefaultPdndMetadataExtractor();

        ((Action)(() => extractor.Extract(null!, new PdndMetadataOptions()))).Should().Throw<ArgumentNullException>();
        ((Action)(() => extractor.Extract(new PdndRequestContext(), null!))).Should().Throw<ArgumentNullException>();
    }
}
