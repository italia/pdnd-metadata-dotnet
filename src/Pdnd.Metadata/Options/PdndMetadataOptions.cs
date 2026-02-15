// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Options;

/// <summary>
/// Configures how metadata is extracted and normalized.
/// </summary>
public sealed class PdndMetadataOptions
{
    /// <summary>
    /// Gets or sets whether all headers should be captured as raw metadata items.
    /// When enabled, headers are still subject to <see cref="HeaderDenyList"/>.
    /// </summary>
    public bool CaptureAllHeaders { get; set; } = true;

    /// <summary>
    /// Gets the list of header names that must never be captured as raw metadata (e.g., secrets).
    /// Comparisons are case-insensitive.
    /// </summary>
    public ISet<string> HeaderDenyList { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Do not store raw tokens/cookies in metadata.
        "Authorization",
        "Cookie",
        "Set-Cookie"
    };

    /// <summary>
    /// Gets the allow-list of header names to capture when <see cref="CaptureAllHeaders"/> is false.
    /// Comparisons are case-insensitive.
    /// </summary>
    public ISet<string> HeaderAllowList { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "User-Agent",
        "X-Request-Id",
        "X-Correlation-Id",
        "Traceparent",
        "Tracestate",
        "Baggage",
        "Forwarded",
        "X-Forwarded-For",
        "X-Forwarded-Proto",
        "X-Forwarded-Host",

        // PDND-related headers (raw capture may be controlled separately):
        "Agid-JWT-Tracking-Evidence",
        "AgID-JWT-TrackingEvidence",
        "Digest",
        "DPoP"
    };

    /// <summary>
    /// Gets or sets the maximum number of values per header to capture.
    /// </summary>
    public int MaxHeaderValuesPerName { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum length for a captured value. Longer values are truncated.
    /// </summary>
    public int MaxValueLength { get; set; } = 2048;

    /// <summary>
    /// Gets or sets a maximum length for JWT/JWS-like tokens to be decoded.
    /// This prevents best-effort parsing from processing unusually large inputs.
    /// </summary>
    public int MaxTokenLength { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets whether well-known tracing headers should be promoted to canonical keys.
    /// </summary>
    public bool PromoteTracingHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether forwarded headers should be parsed and normalized.
    /// </summary>
    public bool NormalizeForwardedHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the library should parse the Authorization Bearer token as a JWT
    /// and extract PDND voucher fields (best-effort, no signature validation).
    /// Raw Authorization header is never stored.
    /// </summary>
    public bool ParsePdndVoucherFromAuthorizationBearer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the library should parse the PDND Tracking Evidence header as a JWS/JWT
    /// and extract relevant fields (best-effort, no signature validation).
    /// </summary>
    public bool ParsePdndTrackingEvidence { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the library should parse the DPoP header as a JWS/JWT-like token
    /// and extract relevant fields (best-effort, no signature validation).
    /// </summary>
    public bool ParseDpopHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the library should parse the Digest header (best-effort) and normalize it.
    /// </summary>
    public bool ParseDigestHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether raw Tracking Evidence header should be captured as http.header.* metadata.
    /// Strongly consider leaving this false to avoid storing signed blobs.
    /// </summary>
    public bool CaptureRawTrackingEvidenceHeader { get; set; } = false;

    /// <summary>
    /// Gets or sets whether raw DPoP header should be captured as http.header.* metadata.
    /// Strongly consider leaving this false to avoid storing signed proofs.
    /// </summary>
    public bool CaptureRawDpopHeader { get; set; } = false;

    /// <summary>
    /// Gets or sets whether raw Digest header should be captured as http.header.* metadata.
    /// </summary>
    public bool CaptureRawDigestHeader { get; set; } = true;
}