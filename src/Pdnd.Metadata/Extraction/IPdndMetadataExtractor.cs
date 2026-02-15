// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;

namespace Pdnd.Metadata.Extraction;

/// <summary>
/// Extracts caller/request metadata from a transport-agnostic request context.
/// </summary>
public interface IPdndMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from the given request context.
    /// </summary>
    /// <param name="context">Transport-agnostic request context.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>A metadata snapshot.</returns>
    PdndCallerMetadata Extract(PdndRequestContext context, PdndMetadataOptions options);
}