// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Models;

/// <summary>
/// Represents a single metadata entry extracted from an incoming request.
/// </summary>
/// <param name="Key">Canonical metadata key.</param>
/// <param name="Value">Metadata value.</param>
/// <param name="Source">Origin of the value.</param>
public sealed record PdndMetadataItem(
    string Key,
    string Value,
    PdndMetadataSource Source);