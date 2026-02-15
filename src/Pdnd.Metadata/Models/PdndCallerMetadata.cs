// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Collections.ObjectModel;

namespace Pdnd.Metadata.Models;

/// <summary>
/// Aggregates metadata about the caller/request in a normalized structure.
/// </summary>
public sealed class PdndCallerMetadata
{
    private readonly Dictionary<string, List<PdndMetadataItem>> _items =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the UTC timestamp when this metadata snapshot was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a read-only view of all metadata items grouped by key.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PdndMetadataItem>> Items
        => _items.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<PdndMetadataItem>)new ReadOnlyCollection<PdndMetadataItem>(kvp.Value),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a metadata item to the snapshot.
    /// </summary>
    /// <param name="item">The metadata item to add.</param>
    public void Add(PdndMetadataItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Key))
            return;

        if (!_items.TryGetValue(item.Key, out var list))
        {
            list = new List<PdndMetadataItem>();
            _items[item.Key] = list;
        }

        list.Add(item);
    }

    /// <summary>
    /// Adds a metadata value using the provided attributes.
    /// </summary>
    /// <param name="key">Canonical metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <param name="source">Origin of the value.</param>
    public void Add(string key, string value, PdndMetadataSource source)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;

        Add(new PdndMetadataItem(key, value, source));
    }

    /// <summary>
    /// Gets the first value for a given key, if any.
    /// </summary>
    /// <param name="key">Canonical metadata key.</param>
    /// <returns>The first value if present; otherwise <c>null</c>.</returns>
    public string? GetFirstValue(string key)
    {
        if (_items.TryGetValue(key, out var list) && list.Count > 0)
            return list[0].Value;

        return null;
    }

    /// <summary>
    /// Gets all values for a given key.
    /// </summary>
    /// <param name="key">Canonical metadata key.</param>
    /// <returns>All values; an empty sequence if not present.</returns>
    public IEnumerable<string> GetValues(string key)
    {
        if (_items.TryGetValue(key, out var list))
            return list.Select(x => x.Value);

        return Array.Empty<string>();
    }

    /// <summary>
    /// Creates a shallow copy (useful for immutability boundaries).
    /// </summary>
    /// <returns>A new <see cref="PdndCallerMetadata"/> containing the same items.</returns>
    public PdndCallerMetadata Clone()
    {
        var clone = new PdndCallerMetadata();
        foreach (var kvp in _items)
        {
            foreach (var item in kvp.Value)
                clone.Add(item);
        }

        return clone;
    }
}