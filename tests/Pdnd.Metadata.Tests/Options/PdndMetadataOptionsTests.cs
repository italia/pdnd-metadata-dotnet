// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Options;

namespace Pdnd.Metadata.Tests.Options;

public class PdndMetadataOptionsTests
{
    [Fact]
    public void Defaults_ShouldRetainNothingThatWasNotAskedFor()
    {
        var opts = new PdndMetadataOptions();

        // Retention is opt-in. Capturing every header keeps any bespoke credential header the
        // deny list does not know about, so enabling it has to be a deliberate decision.
        opts.CaptureAllHeaders.Should().BeFalse();

        // Signed blobs are decoded for their claims but the raw token is not kept.
        opts.CaptureRawTrackingEvidenceHeader.Should().BeFalse();
        opts.CaptureRawDpopHeader.Should().BeFalse();
        opts.CaptureRawSignatureHeader.Should().BeFalse();

        // The digest is a hash of the body, not a credential, so it is kept.
        opts.CaptureRawDigestHeader.Should().BeTrue();

        // Nothing is excluded until the consumer says so.
        opts.PathExclusions.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_ShouldExtractEverythingPdndSends()
    {
        var opts = new PdndMetadataOptions();

        // Parsing is not retention: these read headers and produce claims, they do not store
        // the tokens. Turning them off costs observability and buys nothing.
        opts.ParsePdndVoucherFromAuthorizationBearer.Should().BeTrue();
        opts.ParsePdndTrackingEvidence.Should().BeTrue();
        opts.ParseDpopHeader.Should().BeTrue();
        opts.ParseDigestHeader.Should().BeTrue();
        opts.ParseContentDigestHeader.Should().BeTrue();
        opts.ParseAgidJwtSignature.Should().BeTrue();

        opts.PromoteTracingHeaders.Should().BeTrue();
        opts.NormalizeForwardedHeaders.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldBoundEveryDimensionThatACallerControls()
    {
        var opts = new PdndMetadataOptions();

        opts.MaxHeaderCount.Should().BeGreaterThan(0);
        opts.MaxHeaderValuesPerName.Should().BeGreaterThan(0);
        opts.MaxValueLength.Should().BeGreaterThan(0);
        opts.MaxTokenLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DenyList_ShouldCoverCredentialBearingHeaderNames()
    {
        var denyList = new PdndMetadataOptions().HeaderDenyList;

        denyList.Should().Contain("Authorization");
        denyList.Should().Contain("Proxy-Authorization");
        denyList.Should().Contain("Cookie");
        denyList.Should().Contain("X-Api-Key");
    }

    [Fact]
    public void HeaderDenyList_ShouldContainAuthorizationCookieAndSetCookie_ByDefault()
    {
        var opts = new PdndMetadataOptions();

        opts.HeaderDenyList.Should().Contain("Authorization");
        opts.HeaderDenyList.Should().Contain("Cookie");
        opts.HeaderDenyList.Should().Contain("Set-Cookie");
    }

    [Fact]
    public void HeaderAllowList_ShouldContainCommonTracingAndPdndHeaders_ByDefault()
    {
        var opts = new PdndMetadataOptions();

        opts.HeaderAllowList.Should().Contain("Traceparent");
        opts.HeaderAllowList.Should().Contain("X-Correlation-Id");
        opts.HeaderAllowList.Should().Contain("X-Request-Id");
        opts.HeaderAllowList.Should().Contain("Forwarded");
        opts.HeaderAllowList.Should().Contain("Agid-JWT-Tracking-Evidence");
        opts.HeaderAllowList.Should().Contain("Digest");
        opts.HeaderAllowList.Should().Contain("DPoP");
    }

    [Fact]
    public void HeaderDenyList_ShouldBeCaseInsensitive()
    {
        var opts = new PdndMetadataOptions();

        opts.HeaderDenyList.Contains("authorization").Should().BeTrue();
        opts.HeaderDenyList.Contains("AUTHORIZATION").Should().BeTrue();
    }

    [Fact]
    public void HeaderAllowList_ShouldBeCaseInsensitive()
    {
        var opts = new PdndMetadataOptions();

        opts.HeaderAllowList.Contains("traceparent").Should().BeTrue();
        opts.HeaderAllowList.Contains("TRACEPARENT").Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxHeaderValuesPerName_ShouldThrow_WhenLessThanOne(int invalid)
    {
        var opts = new PdndMetadataOptions();

        var act = () => opts.MaxHeaderValuesPerName = invalid;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PdndMetadataOptions.MaxHeaderValuesPerName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxValueLength_ShouldThrow_WhenLessThanOne(int invalid)
    {
        var opts = new PdndMetadataOptions();

        var act = () => opts.MaxValueLength = invalid;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PdndMetadataOptions.MaxValueLength));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxTokenLength_ShouldThrow_WhenLessThanOne(int invalid)
    {
        var opts = new PdndMetadataOptions();

        var act = () => opts.MaxTokenLength = invalid;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PdndMetadataOptions.MaxTokenLength));
    }

    [Fact]
    public void MaxLimits_ShouldAcceptPositiveValues()
    {
        var opts = new PdndMetadataOptions
        {
            MaxHeaderValuesPerName = 5,
            MaxValueLength = 100,
            MaxTokenLength = 4_000
        };

        opts.MaxHeaderValuesPerName.Should().Be(5);
        opts.MaxValueLength.Should().Be(100);
        opts.MaxTokenLength.Should().Be(4_000);
    }
}
