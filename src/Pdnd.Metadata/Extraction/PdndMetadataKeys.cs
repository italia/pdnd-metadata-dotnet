// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction;

/// <summary>
/// Canonical keys used by the extractor.
/// </summary>
public static class PdndMetadataKeys
{
    /// <summary>HTTP method.</summary>
    public const string HttpMethod = "http.method";

    /// <summary>HTTP scheme.</summary>
    public const string HttpScheme = "http.scheme";

    /// <summary>HTTP host.</summary>
    public const string HttpHost = "http.host";

    /// <summary>HTTP path.</summary>
    public const string HttpPath = "http.path";

    /// <summary>HTTP query string.</summary>
    public const string HttpQuery = "http.query";

    /// <summary>Remote IP address.</summary>
    public const string NetRemoteIp = "net.remote_ip";

    /// <summary>Remote port.</summary>
    public const string NetRemotePort = "net.remote_port";

    /// <summary>Local IP address.</summary>
    public const string NetLocalIp = "net.local_ip";

    /// <summary>Local port.</summary>
    public const string NetLocalPort = "net.local_port";

    /// <summary>Normalized forwarded chain (if available).</summary>
    public const string NetForwardedFor = "net.forwarded_for";

    /// <summary>Correlation/request identifier.</summary>
    public const string CorrelationId = "correlation.id";

    /// <summary>W3C traceparent.</summary>
    public const string TraceParent = "trace.traceparent";

    /// <summary>W3C tracestate.</summary>
    public const string TraceState = "trace.tracestate";

    /// <summary>W3C baggage.</summary>
    public const string TraceBaggage = "trace.baggage";

    /// <summary>Raw header prefix (canonical).</summary>
    public const string HeaderPrefix = "http.header.";

    /// <summary>Request ID.</summary>
    public const string RequestId = "request.id";

    // -------------------------
    // PDND canonical keys
    // -------------------------

    /// <summary>Voucher JWT issuer.</summary>
    public const string PdndVoucherIss = "pdnd.voucher.iss";

    /// <summary>Voucher JWT subject.</summary>
    public const string PdndVoucherSub = "pdnd.voucher.sub";

    /// <summary>Voucher JWT audience (may be multiple).</summary>
    public const string PdndVoucherAud = "pdnd.voucher.aud";

    /// <summary>Voucher JWT id.</summary>
    public const string PdndVoucherJti = "pdnd.voucher.jti";

    /// <summary>Voucher JWT issued at (epoch seconds if possible).</summary>
    public const string PdndVoucherIat = "pdnd.voucher.iat";

    /// <summary>Voucher JWT not-before (epoch seconds if possible).</summary>
    public const string PdndVoucherNbf = "pdnd.voucher.nbf";

    /// <summary>Voucher JWT expiration (epoch seconds if possible).</summary>
    public const string PdndVoucherExp = "pdnd.voucher.exp";

    /// <summary>Voucher purposeId claim (if present).</summary>
    public const string PdndVoucherPurposeId = "pdnd.voucher.purposeId";

    /// <summary>Voucher client id / subject-like identifier (camelCase, if present).</summary>
    public const string PdndVoucherClientId = "pdnd.voucher.clientId";

    /// <summary>Voucher client_id (OAuth 2.0 standard underscore form, if present).</summary>
    public const string PdndVoucherClientIdUnderscore = "pdnd.voucher.client_id";

    /// <summary>Tracking evidence JWS/JWT header alg.</summary>
    public const string PdndTrackingAlg = "pdnd.trackingEvidence.alg";

    /// <summary>Tracking evidence JWS/JWT header kid.</summary>
    public const string PdndTrackingKid = "pdnd.trackingEvidence.kid";

    /// <summary>Tracking evidence JWS/JWT header typ.</summary>
    public const string PdndTrackingTyp = "pdnd.trackingEvidence.typ";

    /// <summary>Tracking evidence payload issuer (if present).</summary>
    public const string PdndTrackingIss = "pdnd.trackingEvidence.iss";

    /// <summary>Tracking evidence payload subject (if present).</summary>
    public const string PdndTrackingSub = "pdnd.trackingEvidence.sub";

    /// <summary>Tracking evidence payload jti (if present).</summary>
    public const string PdndTrackingJti = "pdnd.trackingEvidence.jti";

    /// <summary>Tracking evidence payload audience (if present).</summary>
    public const string PdndTrackingAud = "pdnd.trackingEvidence.aud";

    /// <summary>Tracking evidence payload issued at (if present).</summary>
    public const string PdndTrackingIat = "pdnd.trackingEvidence.iat";

    /// <summary>Tracking evidence payload expiration (if present).</summary>
    public const string PdndTrackingExp = "pdnd.trackingEvidence.exp";

    /// <summary>Normalized Digest algorithm (best-effort).</summary>
    public const string PdndDigestAlg = "pdnd.digest.alg";

    /// <summary>Normalized Digest value (best-effort).</summary>
    public const string PdndDigestValue = "pdnd.digest.value";

    // -------------------------
    // DPoP canonical keys
    // -------------------------

    /// <summary>DPoP proof header alg.</summary>
    public const string PdndDpopAlg = "pdnd.dpop.alg";

    /// <summary>DPoP proof header kid.</summary>
    public const string PdndDpopKid = "pdnd.dpop.kid";

    /// <summary>DPoP proof header typ.</summary>
    public const string PdndDpopTyp = "pdnd.dpop.typ";

    /// <summary>DPoP proof HTTP method claim (htm) if present.</summary>
    public const string PdndDpopHtm = "pdnd.dpop.htm";

    /// <summary>DPoP proof HTTP target URI claim (htu) if present.</summary>
    public const string PdndDpopHtu = "pdnd.dpop.htu";

    /// <summary>DPoP proof JWT id (jti) if present.</summary>
    public const string PdndDpopJti = "pdnd.dpop.jti";

    /// <summary>DPoP proof issued at (iat) if present.</summary>
    public const string PdndDpopIat = "pdnd.dpop.iat";
}