// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Models;

/// <summary>
/// Describes where a metadata value has been extracted from.
/// </summary>
public enum PdndMetadataSource
{
    /// <summary>HTTP headers (including forwarded headers).</summary>
    Header = 0,

    /// <summary>Transport / connection details (remote IP, protocol, etc.).</summary>
    Connection = 1,

    /// <summary>TLS/mTLS related information (if provided by the hosting environment).</summary>
    Tls = 2,

    /// <summary>Authenticated user claims (if present on the request principal).</summary>
    Claims = 3,

    /// <summary>Distributed tracing context (traceparent, baggage, etc.).</summary>
    Tracing = 4,

    /// <summary>Computed or derived value (e.g., normalized IP chain).</summary>
    Derived = 5
}