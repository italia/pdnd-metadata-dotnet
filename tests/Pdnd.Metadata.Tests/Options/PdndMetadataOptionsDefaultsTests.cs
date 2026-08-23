// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;

namespace Pdnd.Metadata.Tests.Options;

/// <summary>
/// Covers the capture posture introduced in 2.0.0: capture is opt-in, the deny list is a
/// backstop, the number of distinct headers is bounded, and truncation is observable.
/// </summary>
public sealed class PdndMetadataOptionsDefaultsTests
{
    private static PdndCallerMetadata Extract(PdndMetadataOptions options, params (string Name, string Value)[] headers)
    {
        var context = new PdndRequestContext
        {
            Method = "GET",
            Scheme = "https",
            Host = "erogatore.example.it",
            Path = "/v1/resource",
            Headers = headers
                .Select(h => new PdndRequestHeader(h.Name, new[] { h.Value }))
                .ToList()
        };

        return new DefaultPdndMetadataExtractor().Extract(context, options);
    }

    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData("Api-Key")]
    [InlineData("X-Auth-Token")]
    [InlineData("Proxy-Authorization")]
    [InlineData("Authentication")]
    public void Extract_WithDefaults_DoesNotCaptureCredentialHeaders(string headerName)
    {
        var metadata = Extract(new PdndMetadataOptions(), (headerName, "secret-value"));

        metadata.GetFirstValue(PdndMetadataKeys.HeaderPrefix + headerName.ToLowerInvariant())
            .Should().BeNull();
    }

    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData("Proxy-Authorization")]
    public void Extract_WithCaptureAllHeaders_StillBlocksDeniedHeaders(string headerName)
    {
        var options = new PdndMetadataOptions { CaptureAllHeaders = true };

        Extract(options, (headerName, "secret-value"))
            .GetFirstValue(PdndMetadataKeys.HeaderPrefix + headerName.ToLowerInvariant())
            .Should().BeNull();
    }

    [Fact]
    public void Extract_WithDefaults_CapturesAllowListedHeaders()
    {
        Extract(new PdndMetadataOptions(), ("Traceparent", "00-abc-def-01"))
            .GetFirstValue(PdndMetadataKeys.HeaderPrefix + "traceparent")
            .Should().Be("00-abc-def-01");
    }

    [Fact]
    public void Extract_BeyondMaxHeaderCount_StopsAndMarksTruncation()
    {
        var options = new PdndMetadataOptions { CaptureAllHeaders = true, MaxHeaderCount = 3 };
        var headers = Enumerable.Range(0, 25).Select(i => ($"X-Header-{i}", "value")).ToArray();

        var metadata = Extract(options, headers);

        metadata.Snapshot().Keys
            .Count(k => k.StartsWith(PdndMetadataKeys.HeaderPrefix, StringComparison.Ordinal))
            .Should().Be(3);

        metadata.GetFirstValue(PdndMetadataKeys.MetadataTruncated).Should().Be("headers");
    }

    [Fact]
    public void Extract_WithinMaxHeaderCount_DoesNotMarkTruncation()
    {
        var options = new PdndMetadataOptions { CaptureAllHeaders = true };

        Extract(options, ("X-Header", "value"))
            .GetFirstValue(PdndMetadataKeys.MetadataTruncated)
            .Should().BeNull();
    }

    [Fact]
    public void MaxHeaderCount_BelowOne_Throws()
    {
        var act = () => new PdndMetadataOptions { MaxHeaderCount = 0 };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("/health", "/health/ready", true)]
    [InlineData("/health", "/HEALTH", true)]
    [InlineData("/health", "/v1/resource", false)]
    public void IsPathExcluded_MatchesPrefixCaseInsensitively(string exclusion, string path, bool expected)
    {
        var options = new PdndMetadataOptions();
        options.PathExclusions.Add(exclusion);

        options.IsPathExcluded(path).Should().Be(expected);
    }

    [Fact]
    public void IsPathExcluded_WithNoExclusions_ReturnsFalse()
        => new PdndMetadataOptions().IsPathExcluded("/health").Should().BeFalse();
}
