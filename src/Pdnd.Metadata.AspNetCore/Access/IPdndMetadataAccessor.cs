// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.AspNetCore.Access;

/// <summary>
/// Provides access to the current request's PDND metadata snapshot.
/// </summary>
public interface IPdndMetadataAccessor
{
    /// <summary>
    /// Gets the current request metadata snapshot, if available.
    /// </summary>
    PdndCallerMetadata? Current { get; }
}