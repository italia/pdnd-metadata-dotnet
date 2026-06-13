// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Extraction.Pdnd;

namespace Pdnd.Metadata.Tests.Pdnd;

public class PdndHeaderNamesTests
{
    [Theory]
    [InlineData("Agid-JWT-Tracking-Evidence", true)]
    [InlineData("agid-jwt-tracking-evidence", true)]
    [InlineData("AgID-JWT-TrackingEvidence", true)]
    [InlineData("agid-jwt-trackingevidence", true)]
    [InlineData("Authorization", false)]
    [InlineData("DPoP", false)]
    [InlineData("Digest", false)]
    [InlineData("Content-Digest", false)]
    [InlineData("Agid-JWT-Signature", false)]
    [InlineData("", false)]
    public void IsTrackingEvidenceHeader_ShouldMatchBothVariants_CaseInsensitive(string headerName, bool expected)
    {
        PdndHeaderNames.IsTrackingEvidenceHeader(headerName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Digest", true)]
    [InlineData("digest", true)]
    [InlineData("Content-Digest", true)]
    [InlineData("content-digest", true)]
    [InlineData("CONTENT-DIGEST", true)]
    [InlineData("Authorization", false)]
    [InlineData("Agid-JWT-Tracking-Evidence", false)]
    [InlineData("DPoP", false)]
    [InlineData("", false)]
    public void IsDigestHeader_ShouldMatchLegacyAndRfc9530_CaseInsensitive(string headerName, bool expected)
    {
        PdndHeaderNames.IsDigestHeader(headerName).Should().Be(expected);
    }

    [Theory]
    [InlineData("DPoP", true)]
    [InlineData("dpop", true)]
    [InlineData("DPOP", true)]
    [InlineData("Authorization", false)]
    [InlineData("Digest", false)]
    [InlineData("", false)]
    public void IsDpopHeader_ShouldMatchCaseInsensitive(string headerName, bool expected)
    {
        PdndHeaderNames.IsDpopHeader(headerName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Agid-JWT-Signature", true)]
    [InlineData("agid-jwt-signature", true)]
    [InlineData("AGID-JWT-SIGNATURE", true)]
    [InlineData("Agid-JWT-Tracking-Evidence", false)]
    [InlineData("Authorization", false)]
    [InlineData("DPoP", false)]
    [InlineData("", false)]
    public void IsSignatureHeader_ShouldMatchCaseInsensitive(string headerName, bool expected)
    {
        PdndHeaderNames.IsSignatureHeader(headerName).Should().Be(expected);
    }
}
