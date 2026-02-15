// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Net;
using System.Security.Claims;

namespace Pdnd.Metadata.RequestContext;

/// <summary>
/// A transport-agnostic representation of an incoming request.
/// This model is meant to decouple extraction logic from a specific web framework.
/// </summary>
public sealed class PdndRequestContext
{
    /// <summary>HTTP method (e.g., GET, POST).</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Request scheme (http/https), if known.</summary>
    public string? Scheme { get; init; }

    /// <summary>Request host, if known.</summary>
    public string? Host { get; init; }

    /// <summary>Request path, if known.</summary>
    public string? Path { get; init; }

    /// <summary>Raw query string, if known.</summary>
    public string? QueryString { get; init; }

    /// <summary>Remote IP address, if known.</summary>
    public IPAddress? RemoteIpAddress { get; init; }

    /// <summary>Remote port, if known.</summary>
    public int? RemotePort { get; init; }

    /// <summary>Local IP address, if known.</summary>
    public IPAddress? LocalIpAddress { get; init; }

    /// <summary>Local port, if known.</summary>
    public int? LocalPort { get; init; }

    /// <summary>Request headers.</summary>
    public IReadOnlyList<PdndRequestHeader> Headers { get; init; } = Array.Empty<PdndRequestHeader>();

    /// <summary>
    /// Authenticated user claims, if any (can be empty).
    /// </summary>
    public IReadOnlyList<Claim> Claims { get; init; } = Array.Empty<Claim>();

    /// <summary>
    /// Transport security hints (e.g., "TLS1.3", "mTLS").
    /// </summary>
    public IReadOnlyDictionary<string, string> SecurityHints { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Trace context hints (traceparent, tracestate, baggage, correlation id).
    /// </summary>
    public IReadOnlyDictionary<string, string> TracingHints { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}