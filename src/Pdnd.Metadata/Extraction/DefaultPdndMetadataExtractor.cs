// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Extraction.Pdnd;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;

namespace Pdnd.Metadata.Extraction;

/// <summary>
/// Default implementation that captures headers, connection details, trace hints,
/// and (optionally) PDND-specific tokens/headers (best-effort, fail-soft).
/// </summary>
public sealed class DefaultPdndMetadataExtractor : IPdndMetadataExtractor
{
    /// <inheritdoc />
    public PdndCallerMetadata Extract(PdndRequestContext context, PdndMetadataOptions options)
    {
        var md = new PdndCallerMetadata();

        // Basic HTTP info
        if (!string.IsNullOrWhiteSpace(context.Method))
            md.Add(PdndMetadataKeys.HttpMethod, context.Method, PdndMetadataSource.Derived);

        if (!string.IsNullOrWhiteSpace(context.Scheme))
            md.Add(PdndMetadataKeys.HttpScheme, context.Scheme!, PdndMetadataSource.Connection);

        if (!string.IsNullOrWhiteSpace(context.Host))
            md.Add(PdndMetadataKeys.HttpHost, context.Host!, PdndMetadataSource.Connection);

        if (!string.IsNullOrWhiteSpace(context.Path))
            md.Add(PdndMetadataKeys.HttpPath, context.Path!, PdndMetadataSource.Derived);

        if (!string.IsNullOrWhiteSpace(context.QueryString))
            md.Add(PdndMetadataKeys.HttpQuery, Truncate(context.QueryString!, options.MaxValueLength), PdndMetadataSource.Derived);

        // Connection info
        if (context.RemoteIpAddress is not null)
            md.Add(PdndMetadataKeys.NetRemoteIp, context.RemoteIpAddress.ToString(), PdndMetadataSource.Connection);

        if (context.RemotePort is not null)
            md.Add(PdndMetadataKeys.NetRemotePort, context.RemotePort.Value.ToString(), PdndMetadataSource.Connection);

        if (context.LocalIpAddress is not null)
            md.Add(PdndMetadataKeys.NetLocalIp, context.LocalIpAddress.ToString(), PdndMetadataSource.Connection);

        if (context.LocalPort is not null)
            md.Add(PdndMetadataKeys.NetLocalPort, context.LocalPort.Value.ToString(), PdndMetadataSource.Connection);

        // Security hints (best-effort)
        foreach (var kv in context.SecurityHints)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;

            md.Add($"security.{kv.Key}", Truncate(kv.Value, options.MaxValueLength), PdndMetadataSource.Tls);
        }

        // Tracing hints (promoted)
        if (options.PromoteTracingHeaders)
        {
            PromoteTracing(md, context, options);
            PromoteCorrelation(md, context, options);
        }

        // PDND extraction (best-effort, no validation, fail-soft)
        ExtractPdnd(md, context, options);

        // Headers (raw)
        CaptureHeaders(md, context, options);

        // Forwarded headers normalization
        if (options.NormalizeForwardedHeaders)
        {
            NormalizeForwardedFor(md, context, options);
        }

        // Claims (informative; may be empty)
        foreach (var claim in context.Claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value))
                continue;

            md.Add($"claim.{claim.Type}", Truncate(claim.Value, options.MaxValueLength), PdndMetadataSource.Claims);
        }

        return md;
    }

    private static void ExtractPdnd(PdndCallerMetadata md, PdndRequestContext context, PdndMetadataOptions options)
    {
        // 1) Voucher in Authorization: Bearer <JWT>
        if (options.ParsePdndVoucherFromAuthorizationBearer)
        {
            var auth = GetHeaderFirst(context, PdndHeaderNames.Authorization);
            if (PdndAuthorization.TryGetBearerToken(auth, out var bearerToken) &&
                IsTokenLengthAllowed(bearerToken, options) &&
                JwtDecoder.TryDecode(bearerToken!, out var jwt))
            {
                PdndVoucherClaimsExtractor.Extract(md, jwt); // fail-soft internally
            }
        }

        // 2) Tracking evidence header (JWS/JWT)
        if (options.ParsePdndTrackingEvidence)
        {
            var te = GetHeaderFirst(context, PdndHeaderNames.AgidJwtTrackingEvidence)
                     ?? GetHeaderFirst(context, PdndHeaderNames.AgidJwtTrackingEvidenceAlt);

            if (!string.IsNullOrWhiteSpace(te) &&
                IsTokenLengthAllowed(te, options) &&
                JwtDecoder.TryDecode(te!, out var jws))
            {
                PdndTrackingEvidenceExtractor.Extract(md, jws); // fail-soft internally
            }
        }

        // 3) DPoP header (JWS/JWT-like proof)
        if (options.ParseDpopHeader)
        {
            var dpop = GetHeaderFirst(context, PdndHeaderNames.DPoP);
            if (!string.IsNullOrWhiteSpace(dpop) &&
                IsTokenLengthAllowed(dpop, options) &&
                JwtDecoder.TryDecode(dpop!, out var proof))
            {
                PdndDpopExtractor.Extract(md, proof); // fail-soft internally
            }
        }

        // 4) Digest header (best-effort)
        if (options.ParseDigestHeader)
        {
            var digest = GetHeaderFirst(context, PdndHeaderNames.Digest);
            if (!string.IsNullOrWhiteSpace(digest) &&
                DigestParser.TryParseDigestHeader(digest!, out var alg, out var value))
            {
                if (!string.IsNullOrWhiteSpace(alg))
                    md.Add(PdndMetadataKeys.PdndDigestAlg, alg!, PdndMetadataSource.Header);

                if (!string.IsNullOrWhiteSpace(value))
                    md.Add(PdndMetadataKeys.PdndDigestValue, value!, PdndMetadataSource.Header);
            }
        }
    }

    private static bool IsTokenLengthAllowed(string? token, PdndMetadataOptions options)
        => !string.IsNullOrWhiteSpace(token) && token!.Length <= options.MaxTokenLength;

    private static void PromoteTracing(PdndCallerMetadata md, PdndRequestContext context, PdndMetadataOptions options)
    {
        if (context.TracingHints.TryGetValue("traceparent", out var tp) && !string.IsNullOrWhiteSpace(tp))
            md.Add(PdndMetadataKeys.TraceParent, Truncate(tp, options.MaxValueLength), PdndMetadataSource.Tracing);

        if (context.TracingHints.TryGetValue("tracestate", out var ts) && !string.IsNullOrWhiteSpace(ts))
            md.Add(PdndMetadataKeys.TraceState, Truncate(ts, options.MaxValueLength), PdndMetadataSource.Tracing);

        if (context.TracingHints.TryGetValue("baggage", out var bg) && !string.IsNullOrWhiteSpace(bg))
            md.Add(PdndMetadataKeys.TraceBaggage, Truncate(bg, options.MaxValueLength), PdndMetadataSource.Tracing);
    }

    private static void PromoteCorrelation(PdndCallerMetadata md, PdndRequestContext context, PdndMetadataOptions options)
    {
        if (context.TracingHints.TryGetValue("x-correlation-id", out var corr) && !string.IsNullOrWhiteSpace(corr))
            md.Add(PdndMetadataKeys.CorrelationId, Truncate(corr, options.MaxValueLength), PdndMetadataSource.Tracing);

        if (context.TracingHints.TryGetValue("x-request-id", out var req) && !string.IsNullOrWhiteSpace(req))
            md.Add(PdndMetadataKeys.RequestId, Truncate(req, options.MaxValueLength), PdndMetadataSource.Tracing);
    }

    private static void CaptureHeaders(PdndCallerMetadata md, PdndRequestContext context, PdndMetadataOptions options)
    {
        foreach (var h in context.Headers)
        {
            if (string.IsNullOrWhiteSpace(h.Name))
                continue;

            // Special cases: signed blobs can be suppressed from raw capture
            if (PdndHeaderNames.IsTrackingEvidenceHeader(h.Name) && !options.CaptureRawTrackingEvidenceHeader)
                continue;

            if (PdndHeaderNames.IsDpopHeader(h.Name) && !options.CaptureRawDpopHeader)
                continue;

            if (PdndHeaderNames.IsDigestHeader(h.Name) && !options.CaptureRawDigestHeader)
                continue;

            if (options.HeaderDenyList.Contains(h.Name))
                continue;

            if (!options.CaptureAllHeaders && !options.HeaderAllowList.Contains(h.Name))
                continue;

            var values = h.Values?.Take(Math.Max(1, options.MaxHeaderValuesPerName)) ?? Enumerable.Empty<string>();
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v))
                    continue;

                md.Add(
                    PdndMetadataKeys.HeaderPrefix + h.Name.ToLowerInvariant(),
                    Truncate(v, options.MaxValueLength),
                    PdndMetadataSource.Header);
            }
        }
    }

    private static void NormalizeForwardedFor(PdndCallerMetadata md, PdndRequestContext context, PdndMetadataOptions options)
    {
        var forwarded = GetHeaderFirst(context, "Forwarded");
        var xff = GetHeaderFirst(context, "X-Forwarded-For");

        var chain = new List<string>();

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var parts = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var tokens = part.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var token in tokens)
                {
                    if (token.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = token.Substring(4).Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(val))
                            chain.Add(val);
                    }
                }
            }
        }

        if (chain.Count == 0 && !string.IsNullOrWhiteSpace(xff))
        {
            var parts = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            chain.AddRange(parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        if (chain.Count > 0)
        {
            md.Add(
                PdndMetadataKeys.NetForwardedFor,
                Truncate(string.Join(", ", chain), options.MaxValueLength),
                PdndMetadataSource.Derived);
        }
    }

    private static string? GetHeaderFirst(PdndRequestContext context, string name)
    {
        var header = context.Headers.FirstOrDefault(h => name.Equals(h.Name, StringComparison.OrdinalIgnoreCase));
        return header?.Values.FirstOrDefault();
    }

    private static string Truncate(string value, int maxLen)
        => value.Length <= maxLen ? value : value.Substring(0, maxLen);
}