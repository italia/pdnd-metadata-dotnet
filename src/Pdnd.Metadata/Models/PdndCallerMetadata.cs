// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Models;

/// <summary>
/// Aggregates metadata about the caller/request in a normalized structure.
/// </summary>
/// <remarks>
/// In the shipped pipeline the snapshot is built by a single middleware invocation on one thread
/// and read afterwards, so no synchronization is required for that flow. The lock is retained
/// because the type is public and nothing prevents a consumer from adding to it from a
/// background task or a parallel enrichment step.
/// </remarks>
public sealed class PdndCallerMetadata
{
    private readonly Dictionary<string, List<PdndMetadataItem>> _items =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Gets the UTC timestamp when this metadata snapshot was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PdndCallerMetadata"/>.
    /// </summary>
    public PdndCallerMetadata() => CreatedAtUtc = DateTimeOffset.UtcNow;

    private PdndCallerMetadata(DateTimeOffset createdAtUtc) => CreatedAtUtc = createdAtUtc;

    /// <summary>
    /// Returns a point-in-time copy of all metadata items grouped by key.
    /// Items added after the call are not reflected.
    /// </summary>
    /// <remarks>
    /// This is an O(n) copy of the whole snapshot, including an array allocation per key.
    /// It is a method rather than a property so that the cost is visible at the call site:
    /// call it once and reuse the result, do not call it inside a loop.
    /// </remarks>
    /// <returns>A copy of the metadata grouped by canonical key.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<PdndMetadataItem>> Snapshot()
    {
        lock (_lock)
        {
            return _items.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<PdndMetadataItem>)kvp.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets a read-only view of all metadata items grouped by key.
    /// </summary>
    [Obsolete("Renamed to Snapshot(). This property allocates a full copy on every read, which a property name does not convey. Will be removed in 3.0.0.")]
    public IReadOnlyDictionary<string, IReadOnlyList<PdndMetadataItem>> Items => Snapshot();

    /// <summary>
    /// Adds a metadata item to the snapshot.
    /// </summary>
    /// <param name="item">The metadata item to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    public void Add(PdndMetadataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            return;

        lock (_lock)
        {
            if (!_items.TryGetValue(item.Key, out var list))
            {
                list = new List<PdndMetadataItem>();
                _items[item.Key] = list;
            }

            list.Add(item);
        }
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
        lock (_lock)
        {
            if (_items.TryGetValue(key, out var list) && list.Count > 0)
                return list[0].Value;

            return null;
        }
    }

    /// <summary>
    /// Gets all values for a given key.
    /// </summary>
    /// <param name="key">Canonical metadata key.</param>
    /// <returns>All values; an empty sequence if not present.</returns>
    public IEnumerable<string> GetValues(string key)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(key, out var list))
                return list.Select(x => x.Value).ToArray();

            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Creates a shallow copy (useful for immutability boundaries).
    /// </summary>
    /// <returns>A new <see cref="PdndCallerMetadata"/> containing the same items.</returns>
    public PdndCallerMetadata Clone()
    {
        var clone = new PdndCallerMetadata(CreatedAtUtc);
        lock (_lock)
        {
            foreach (var kvp in _items)
            {
                foreach (var item in kvp.Value)
                    clone.Add(item);
            }
        }

        return clone;
    }
}
