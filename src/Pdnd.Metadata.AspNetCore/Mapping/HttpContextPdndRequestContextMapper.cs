// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Pdnd.Metadata.RequestContext;

namespace Pdnd.Metadata.AspNetCore.Mapping;

/// <summary>
/// Maps an ASP.NET Core <see cref="HttpContext"/> into a transport-agnostic <see cref="PdndRequestContext"/>.
/// </summary>
public static class HttpContextPdndRequestContextMapper
{
    /// <summary>
    /// Creates a <see cref="PdndRequestContext"/> from the given <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>A mapped request context.</returns>
    public static PdndRequestContext Map(HttpContext httpContext)
    {
        var req = httpContext.Request;
        var conn = httpContext.Connection;

        var headers = new PdndRequestHeader[req.Headers.Count];
        var index = 0;
        foreach (var header in req.Headers)
            headers[index++] = new PdndRequestHeader(header.Key, NonNullValues(header.Value));

        // The header count is read before enumeration; guard against a collection that does not
        // report it exactly rather than leave null entries behind.
        if (index != headers.Length)
            Array.Resize(ref headers, index);

        var tracing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddHeaderIfPresent(string headerName)
        {
            if (req.Headers.TryGetValue(headerName, out var v) && v.Count > 0)
                tracing[headerName] = v.ToString();
        }

        AddHeaderIfPresent("traceparent");
        AddHeaderIfPresent("tracestate");
        AddHeaderIfPresent("baggage");
        AddHeaderIfPresent("x-correlation-id");
        AddHeaderIfPresent("x-request-id");

        var security = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (req.IsHttps)
            security["https"] = "true";

        var tls = httpContext.Features.Get<ITlsConnectionFeature>();
        if (tls?.ClientCertificate is not null)
        {
            security["mtls.client_certificate_present"] = "true";

            // Best-effort: expose only the certificate thumbprint (no PII like subject/issuer DN).
            try
            {
                var thumbprint = tls.ClientCertificate.Thumbprint;
                if (!string.IsNullOrWhiteSpace(thumbprint))
                    security["mtls.client_certificate_thumbprint"] = thumbprint;
            }
            catch
            {
                // Fail-soft: never break the request due to certificate metadata extraction.
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Protocol))
            security["http.protocol"] = req.Protocol;

        // Best-effort: capture TLS handshake details when the hosting environment exposes them.
        var tlsHandshake = httpContext.Features.Get<ITlsHandshakeFeature>();
        if (tlsHandshake is not null)
        {
            // System.Security.Authentication.SslProtocols values (e.g., "Tls12", "Tls13").
            var protocol = tlsHandshake.Protocol.ToString();
            if (!string.IsNullOrWhiteSpace(protocol) &&
                !protocol.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                security["tls.protocol"] = protocol;
            }
        }

        return new PdndRequestContext
        {
            Method = req.Method,
            Scheme = req.Scheme,
            Host = req.Host.HasValue ? req.Host.Value : null,
            Path = req.Path.HasValue ? req.Path.Value : null,
            QueryString = req.QueryString.HasValue ? req.QueryString.Value : null,
            RemoteIpAddress = conn.RemoteIpAddress,
            RemotePort = conn.RemotePort > 0 ? conn.RemotePort : null,
            LocalIpAddress = conn.LocalIpAddress,
            LocalPort = conn.LocalPort > 0 ? conn.LocalPort : null,
            Headers = headers,
            Claims = httpContext.User?.Claims?.ToArray() ?? Array.Empty<System.Security.Claims.Claim>(),
            SecurityHints = security,
            TracingHints = tracing
        };
    }

    /// <summary>
    /// Copies the header values, dropping null entries, without LINQ. Almost every header has a
    /// single non-null value, which takes the first branch.
    /// </summary>
    private static string[] NonNullValues(StringValues values)
    {
        if (values.Count == 1)
            return values[0] is { } single ? new[] { single } : Array.Empty<string>();

        var nonNull = 0;
        foreach (var value in values)
        {
            if (value is not null)
                nonNull++;
        }

        if (nonNull == 0)
            return Array.Empty<string>();

        var result = new string[nonNull];
        var i = 0;
        foreach (var value in values)
        {
            if (value is not null)
                result[i++] = value;
        }

        return result;
    }
}
