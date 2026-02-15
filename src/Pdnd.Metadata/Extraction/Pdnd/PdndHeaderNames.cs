// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Common header names used in PDND interoperability flows.
/// </summary>
public static class PdndHeaderNames
{
    /// <summary>Authorization header.</summary>
    public const string Authorization = "Authorization";

    /// <summary>Tracking evidence header (common naming).</summary>
    public const string AgidJwtTrackingEvidence = "Agid-JWT-Tracking-Evidence";

    /// <summary>Tracking evidence header (alternate naming found in some environments).</summary>
    public const string AgidJwtTrackingEvidenceAlt = "AgID-JWT-TrackingEvidence";

    /// <summary>Digest header.</summary>
    public const string Digest = "Digest";

    /// <summary>DPoP proof header.</summary>
    public const string DPoP = "DPoP";

    /// <summary>
    /// Checks whether the given header name is a tracking evidence header.
    /// </summary>
    public static bool IsTrackingEvidenceHeader(string headerName)
        => AgidJwtTrackingEvidence.Equals(headerName, StringComparison.OrdinalIgnoreCase)
           || AgidJwtTrackingEvidenceAlt.Equals(headerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given header name is a Digest header.
    /// </summary>
    public static bool IsDigestHeader(string headerName)
        => Digest.Equals(headerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given header name is a DPoP header.
    /// </summary>
    public static bool IsDpopHeader(string headerName)
        => DPoP.Equals(headerName, StringComparison.OrdinalIgnoreCase);
}