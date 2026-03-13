// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

        var headers = req.Headers
            .Select(h => new PdndRequestHeader(h.Key, h.Value.Where(v => v is not null).ToArray()!))
            .ToArray();

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
            security["mtls.client_certificate_present"] = "true";

        if (!string.IsNullOrWhiteSpace(req.Protocol))
            security["http.protocol"] = req.Protocol;

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
}