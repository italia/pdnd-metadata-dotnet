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

    /// <summary>
    /// Tracking evidence header, hyphenated variant. Not the name used by the AgID guidelines
    /// or by PDND, which is <see cref="AgidJwtTrackingEvidenceAlt"/>; kept for compatibility.
    /// </summary>
    public const string AgidJwtTrackingEvidence = "Agid-JWT-Tracking-Evidence";

    /// <summary>
    /// Tracking evidence header as named by the AgID guidelines (AUDIT_REST_01/02) and by PDND,
    /// written <c>Agid-JWT-TrackingEvidence</c> or <c>AgID-JWT-TrackingEvidence</c>: header
    /// names are case-insensitive, so both match.
    /// </summary>
    public const string AgidJwtTrackingEvidenceAlt = "AgID-JWT-TrackingEvidence";

    /// <summary>Digest header.</summary>
    public const string Digest = "Digest";

    /// <summary>DPoP proof header.</summary>
    public const string DPoP = "DPoP";

    /// <summary>Content-Digest header (RFC 9530, replaces Digest).</summary>
    public const string ContentDigest = "Content-Digest";

    /// <summary>Agid-JWT-Signature header (ModI INTEGRITY_REST_01 and INTEGRITY_REST_02).</summary>
    public const string AgidJwtSignature = "Agid-JWT-Signature";

    /// <summary>
    /// Checks whether the given header name is a tracking evidence header.
    /// </summary>
    public static bool IsTrackingEvidenceHeader(string headerName)
        => AgidJwtTrackingEvidence.Equals(headerName, StringComparison.OrdinalIgnoreCase)
           || AgidJwtTrackingEvidenceAlt.Equals(headerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given header name is a Digest header (legacy or RFC 9530).
    /// </summary>
    public static bool IsDigestHeader(string headerName)
        => Digest.Equals(headerName, StringComparison.OrdinalIgnoreCase)
           || ContentDigest.Equals(headerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given header name is a DPoP header.
    /// </summary>
    public static bool IsDpopHeader(string headerName)
        => DPoP.Equals(headerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given header name is an Agid-JWT-Signature header.
    /// </summary>
    public static bool IsSignatureHeader(string headerName)
        => AgidJwtSignature.Equals(headerName, StringComparison.OrdinalIgnoreCase);
}