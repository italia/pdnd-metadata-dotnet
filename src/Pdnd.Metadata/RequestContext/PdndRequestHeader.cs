// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.RequestContext;

/// <summary>
/// Represents a raw HTTP request header.
/// </summary>
/// <param name="Name">Header name.</param>
/// <param name="Values">Header values.</param>
public sealed record PdndRequestHeader(string Name, IReadOnlyList<string> Values);