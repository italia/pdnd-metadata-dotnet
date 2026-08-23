// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Options;

/// <summary>
/// Configures how metadata is extracted and normalized.
/// </summary>
public sealed class PdndMetadataOptions
{
    private readonly HashSet<string> _headerDenyList = new(StringComparer.OrdinalIgnoreCase)
    {
        // Never store raw credentials in metadata. A deny list is a backstop rather than the
        // primary control: the primary control is CaptureAllHeaders being false by default.
        "Authorization",
        "Proxy-Authorization",
        "Authentication",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key",
        "X-Auth-Token",
        "X-Access-Token",
        "X-Csrf-Token"
    };

    private readonly HashSet<string> _headerAllowList = new(StringComparer.OrdinalIgnoreCase)
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
        "Agid-JWT-Signature",
        "Digest",
        "Content-Digest",
        "DPoP"
    };

    /// <summary>
    /// Gets or sets whether all headers should be captured as raw metadata items.
    /// When enabled, headers are still subject to <see cref="HeaderDenyList"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Capturing every inbound header retains any bespoke credential
    /// header the deny list does not know about, so opting in is a deliberate decision.
    /// </remarks>
    public bool CaptureAllHeaders { get; set; } = false;

    /// <summary>
    /// Gets the list of header names that must never be captured as raw metadata (e.g., secrets).
    /// Comparisons are case-insensitive. The underlying set uses <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public ISet<string> HeaderDenyList => _headerDenyList;

    /// <summary>
    /// Gets the allow-list of header names to capture when <see cref="CaptureAllHeaders"/> is false.
    /// Comparisons are case-insensitive. The underlying set uses <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public ISet<string> HeaderAllowList => _headerAllowList;

    private int _maxHeaderCount = 64;
    private int _maxHeaderValuesPerName = 10;
    private int _maxValueLength = 2048;
    private int _maxTokenLength = 16_384;

    /// <summary>
    /// Gets or sets the maximum number of distinct header names to capture.
    /// Once reached, header capture stops and <see cref="Extraction.PdndMetadataKeys.MetadataTruncated"/>
    /// is emitted so that truncation is distinguishable from absence.
    /// Must be at least 1.
    /// </summary>
    /// <remarks>
    /// Kestrel already caps request headers well below this value, so in a default ASP.NET Core
    /// deployment the limit is never reached. It exists because the core package is
    /// transport-agnostic and cannot assume any such upstream bound.
    /// </remarks>
    public int MaxHeaderCount
    {
        get => _maxHeaderCount;
        set => _maxHeaderCount = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(MaxHeaderCount), value, "Value must be at least 1.")
            : value;
    }

    /// <summary>
    /// Gets or sets the maximum number of values per header to capture.
    /// Must be at least 1.
    /// </summary>
    public int MaxHeaderValuesPerName
    {
        get => _maxHeaderValuesPerName;
        set => _maxHeaderValuesPerName = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(MaxHeaderValuesPerName), value, "Value must be at least 1.")
            : value;
    }

    /// <summary>
    /// Gets or sets the maximum length for a captured value. Longer values are truncated.
    /// Must be at least 1.
    /// </summary>
    public int MaxValueLength
    {
        get => _maxValueLength;
        set => _maxValueLength = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(MaxValueLength), value, "Value must be at least 1.")
            : value;
    }

    /// <summary>
    /// Gets or sets a maximum length for JWT/JWS-like tokens to be decoded.
    /// This prevents best-effort parsing from processing unusually large inputs.
    /// Must be at least 1.
    /// </summary>
    public int MaxTokenLength
    {
        get => _maxTokenLength;
        set => _maxTokenLength = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(MaxTokenLength), value, "Value must be at least 1.")
            : value;
    }

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
    /// Gets or sets whether the library should parse the Content-Digest header (RFC 9530, best-effort) and normalize it.
    /// Content-Digest replaces the legacy Digest header in modern PDND implementations.
    /// </summary>
    public bool ParseContentDigestHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the library should parse the Agid-JWT-Signature header as a JWS/JWT-like token
    /// and extract relevant fields (best-effort, no signature validation).
    /// Used in PDND flows for request signing (integrity).
    /// </summary>
    public bool ParseAgidJwtSignature { get; set; } = true;

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

    /// <summary>
    /// Gets or sets whether raw Agid-JWT-Signature header should be captured as http.header.* metadata.
    /// Strongly consider leaving this false to avoid storing signed blobs.
    /// </summary>
    public bool CaptureRawSignatureHeader { get; set; } = false;

    /// <summary>
    /// Gets the request path prefixes for which extraction is skipped entirely.
    /// Comparisons are case-insensitive and prefix-based.
    /// </summary>
    /// <remarks>
    /// Health probes, metrics scrapes, and static files pay the full extraction cost and produce
    /// a snapshot nobody reads. Populate this to skip them.
    /// </remarks>
    public IList<string> PathExclusions { get; } = new List<string>();

    /// <summary>
    /// Returns whether extraction should be skipped for the given request path.
    /// </summary>
    /// <param name="path">Request path, for example <c>/health</c>.</param>
    /// <returns><c>true</c> when the path matches an exclusion prefix.</returns>
    public bool IsPathExcluded(string? path)
    {
        if (string.IsNullOrEmpty(path) || PathExclusions.Count == 0)
            return false;

        for (var i = 0; i < PathExclusions.Count; i++)
        {
            var prefix = PathExclusions[i];
            if (!string.IsNullOrEmpty(prefix) &&
                path!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
