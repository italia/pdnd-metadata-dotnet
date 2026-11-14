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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var md = new PdndCallerMetadata();

        // Built once: the PDND extraction path looks up at least nine well-known headers,
        // and a linear scan per lookup is paid on every request.
        var headerLookup = BuildHeaderLookup(context);

        // Basic HTTP info
        if (!string.IsNullOrWhiteSpace(context.Method))
            md.Add(PdndMetadataKeys.HttpMethod, context.Method, PdndMetadataSource.Derived);

        if (!string.IsNullOrWhiteSpace(context.Scheme))
            md.Add(PdndMetadataKeys.HttpScheme, context.Scheme!, PdndMetadataSource.Connection);

        if (!string.IsNullOrWhiteSpace(context.Host))
            md.Add(PdndMetadataKeys.HttpHost, Truncate(context.Host!, options.MaxValueLength), PdndMetadataSource.Connection);

        if (!string.IsNullOrWhiteSpace(context.Path))
            md.Add(PdndMetadataKeys.HttpPath, Truncate(context.Path!, options.MaxValueLength), PdndMetadataSource.Derived);

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
        ExtractPdnd(md, headerLookup, options);

        // Headers (raw)
        CaptureHeaders(md, context, options);

        // Forwarded headers normalization
        if (options.NormalizeForwardedHeaders)
        {
            NormalizeForwardedFor(md, headerLookup, options);
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

    private static void ExtractPdnd(PdndCallerMetadata md, IReadOnlyDictionary<string, IReadOnlyList<string>> headers, PdndMetadataOptions options)
    {
        // One scratch snapshot for every token of the request, created on first use and
        // cleared between tokens.
        PdndCallerMetadata? scratch = null;

        // 1) Voucher in Authorization: Bearer <JWT> or DPoP <JWT> (DPoP-bound voucher)
        if (options.ParsePdndVoucherFromAuthorizationBearer)
        {
            var auth = GetHeaderFirst(headers, PdndHeaderNames.Authorization);
            if (PdndAuthorization.TryGetVoucherToken(auth, out var bearerToken) &&
                IsTokenLengthAllowed(bearerToken, options) &&
                JwtDecoder.TryDecode(bearerToken!, out var jwt))
            {
                ExtractTruncated(md, ref scratch, options, scratch => PdndVoucherClaimsExtractor.Extract(scratch, jwt!)); // fail-soft internally
            }
        }

        // 2) Tracking evidence header (JWS/JWT)
        if (options.ParsePdndTrackingEvidence)
        {
            var te = GetHeaderFirst(headers, PdndHeaderNames.AgidJwtTrackingEvidence)
                     ?? GetHeaderFirst(headers, PdndHeaderNames.AgidJwtTrackingEvidenceAlt);

            if (!string.IsNullOrWhiteSpace(te) &&
                IsTokenLengthAllowed(te, options) &&
                JwtDecoder.TryDecode(te!, out var jws))
            {
                ExtractTruncated(md, ref scratch, options, scratch => PdndTrackingEvidenceExtractor.Extract(scratch, jws!)); // fail-soft internally
            }
        }

        // 3) DPoP header (JWS/JWT-like proof)
        if (options.ParseDpopHeader)
        {
            var dpop = GetHeaderFirst(headers, PdndHeaderNames.DPoP);
            if (!string.IsNullOrWhiteSpace(dpop) &&
                IsTokenLengthAllowed(dpop, options) &&
                JwtDecoder.TryDecode(dpop!, out var proof))
            {
                ExtractTruncated(md, ref scratch, options, scratch => PdndDpopExtractor.Extract(scratch, proof!)); // fail-soft internally
            }
        }

        // 4) Digest header (best-effort)
        if (options.ParseDigestHeader)
        {
            var digest = GetHeaderFirst(headers, PdndHeaderNames.Digest);
            if (!string.IsNullOrWhiteSpace(digest) &&
                DigestParser.TryParseDigestHeader(digest!, out var alg, out var value))
            {
                if (!string.IsNullOrWhiteSpace(alg))
                    md.Add(PdndMetadataKeys.PdndDigestAlg, Truncate(alg!, options.MaxValueLength), PdndMetadataSource.Header);

                if (!string.IsNullOrWhiteSpace(value))
                    md.Add(PdndMetadataKeys.PdndDigestValue, Truncate(value!, options.MaxValueLength), PdndMetadataSource.Header);
            }
        }

        // 5) Content-Digest header (RFC 9530, replaces legacy Digest)
        if (options.ParseContentDigestHeader)
        {
            var contentDigest = GetHeaderFirst(headers, PdndHeaderNames.ContentDigest);
            if (!string.IsNullOrWhiteSpace(contentDigest) &&
                DigestParser.TryParseContentDigestHeader(contentDigest!, out var cdAlg, out var cdValue))
            {
                if (!string.IsNullOrWhiteSpace(cdAlg))
                    md.Add(PdndMetadataKeys.PdndContentDigestAlg, Truncate(cdAlg!, options.MaxValueLength), PdndMetadataSource.Header);

                if (!string.IsNullOrWhiteSpace(cdValue))
                    md.Add(PdndMetadataKeys.PdndContentDigestValue, Truncate(cdValue!, options.MaxValueLength), PdndMetadataSource.Header);
            }
        }

        // 6) Agid-JWT-Signature header (PDND request signing)
        if (options.ParseAgidJwtSignature)
        {
            var sig = GetHeaderFirst(headers, PdndHeaderNames.AgidJwtSignature);
            if (!string.IsNullOrWhiteSpace(sig) &&
                IsTokenLengthAllowed(sig, options) &&
                JwtDecoder.TryDecode(sig!, out var sigToken))
            {
                ExtractTruncated(md, ref scratch, options, scratch => PdndSignatureExtractor.Extract(scratch, sigToken!)); // fail-soft internally
            }
        }
    }

    /// <summary>
    /// Runs a token extractor into a scratch snapshot and copies the result, truncated to
    /// <see cref="PdndMetadataOptions.MaxValueLength"/>. The extractors are public and take no
    /// options, so the bound is applied here rather than inside each of them.
    /// </summary>
    private static void ExtractTruncated(
        PdndCallerMetadata md,
        ref PdndCallerMetadata? scratch,
        PdndMetadataOptions options,
        Action<PdndCallerMetadata> extract)
    {
        if (scratch is null)
            scratch = new PdndCallerMetadata();
        else
            scratch.Clear();

        extract(scratch);

        // Copies the items directly instead of going through Snapshot(), which would build a
        // dictionary and an array per key only to be thrown away. Untruncated items are reused.
        scratch.CopyTo(md, options.MaxValueLength, Truncate);
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
        var captured = 0;

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

            if (PdndHeaderNames.IsSignatureHeader(h.Name) && !options.CaptureRawSignatureHeader)
                continue;

            if (options.HeaderDenyList.Contains(h.Name))
                continue;

            if (!options.CaptureAllHeaders && !options.HeaderAllowList.Contains(h.Name))
                continue;

            // The bound is checked only for a header that would actually be captured. Checking it
            // before the filters reported truncation when every remaining header was going to be
            // skipped anyway, which made a complete snapshot look incomplete.
            if (captured >= options.MaxHeaderCount)
            {
                md.Add(PdndMetadataKeys.MetadataTruncated, "headers", PdndMetadataSource.Derived);
                break;
            }

            captured++;

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

    private static void NormalizeForwardedFor(PdndCallerMetadata md, IReadOnlyDictionary<string, IReadOnlyList<string>> headers, PdndMetadataOptions options)
    {
        var chain = new List<string>();

        // Every value counts, not only the first: each proxy may append its own header line
        // rather than extend an existing one, and the chain is the concatenation of all of them.
        foreach (var forwarded in GetHeaderAll(headers, "Forwarded"))
        {
            if (string.IsNullOrWhiteSpace(forwarded))
                continue;

            var parts = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var tokens = part.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var token in tokens)
                {
                    if (token.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = token.Substring(4).Trim().Trim('"');

                        // Remove IPv6 brackets if present (RFC 7239 §4)
                        if (val.Length >= 2 && val[0] == '[')
                        {
                            var close = val.IndexOf(']');
                            if (close > 1)
                                val = val.Substring(1, close - 1);
                        }

                        if (!string.IsNullOrWhiteSpace(val))
                            chain.Add(val);
                    }
                }
            }
        }

        if (chain.Count == 0)
        {
            foreach (var xff in GetHeaderAll(headers, "X-Forwarded-For"))
            {
                if (string.IsNullOrWhiteSpace(xff))
                    continue;

                var parts = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                chain.AddRange(parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        if (chain.Count > 0)
        {
            md.Add(
                PdndMetadataKeys.NetForwardedFor,
                Truncate(string.Join(", ", chain), options.MaxValueLength),
                PdndMetadataSource.Derived);
        }
    }

    /// <summary>
    /// Groups every header value by case-insensitive name, preserving arrival order. Built once
    /// per request: the PDND extraction path looks up at least nine well-known headers.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> BuildHeaderLookup(PdndRequestContext context)
    {
        var lookup = new Dictionary<string, IReadOnlyList<string>>(context.Headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var header in context.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Name) || header.Values is null)
                continue;

            // The common case, one entry per name, stores the caller's list as is: no copy.
            if (!lookup.TryGetValue(header.Name, out var existing))
            {
                lookup[header.Name] = header.Values;
                continue;
            }

            // A repeated name is rare. Merge into a new list, never into the caller's.
            var merged = new List<string>(existing.Count + header.Values.Count);
            merged.AddRange(existing);
            merged.AddRange(header.Values);
            lookup[header.Name] = merged;
        }

        return lookup;
    }

    /// <summary>First value of a header. Token headers use the first value only.</summary>
    private static string? GetHeaderFirst(IReadOnlyDictionary<string, IReadOnlyList<string>> headers, string name)
        => headers.TryGetValue(name, out var values) && values.Count > 0 ? values[0] : null;

    /// <summary>Every value of a header, across repeated lines, in arrival order.</summary>
    private static IReadOnlyList<string> GetHeaderAll(IReadOnlyDictionary<string, IReadOnlyList<string>> headers, string name)
        => headers.TryGetValue(name, out var values) ? values : Array.Empty<string>();

    /// <summary>
    /// Truncates to at most <paramref name="maxLen"/> UTF-16 code units without splitting a
    /// surrogate pair: a lone high surrogate is invalid UTF-16 and can make a downstream encoder
    /// or log sink substitute or reject the whole value.
    /// </summary>
    private static string Truncate(string value, int maxLen)
    {
        if (maxLen <= 0)
            return string.Empty;

        if (value.Length <= maxLen)
            return value;

        var cut = char.IsHighSurrogate(value[maxLen - 1]) ? maxLen - 1 : maxLen;
        return value.Substring(0, cut);
    }
}
