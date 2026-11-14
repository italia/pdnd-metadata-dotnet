// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Collections.Concurrent;

namespace Pdnd.Metadata.Diagnostics;

/// <summary>
/// Configures how <see cref="PdndInstrumentation"/> dimensions the request counter.
/// </summary>
/// <remarks>
/// The counter dimensions come from voucher claims, and the extraction pipeline does not verify
/// the voucher signature. A caller can therefore put any value in them. Left unbounded, a stream
/// of forged tokens with random identifiers turns every request into a new time series and
/// exhausts the metrics backend. The bound below caps that, and it is shared by every request
/// recorded with the same options instance.
/// </remarks>
public sealed class PdndDiagnosticsOptions
{
    /// <summary>Value recorded for a dimension whose claim is absent.</summary>
    public const string UnknownValue = "unknown";

    /// <summary>Value recorded for a dimension once its distinct-value bound is reached.</summary>
    public const string OverflowValue = "other";

    private int _maxDistinctValuesPerDimension = 100;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _seen =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether the request counter is dimensioned by consumer identity
    /// (consumer, purpose, client). When <c>false</c> the counter is recorded without
    /// dimensions. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeConsumerDimensions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of distinct values recorded per dimension. Values first
    /// seen after the bound is reached are recorded as <see cref="OverflowValue"/>.
    /// Must be at least 1. Defaults to <c>100</c>.
    /// </summary>
    /// <remarks>
    /// The bound is approximate under concurrency: a few requests racing on the last free slot
    /// may each claim it. It never grows without limit.
    /// <para>
    /// The first values seen keep their slots for the life of the options instance. Because the
    /// claims are not verified, a burst of forged identifiers right after startup can fill every
    /// slot, after which genuine consumers are recorded as <see cref="OverflowValue"/> until the
    /// process restarts. Enable the dimensions where vouchers are validated before they reach the
    /// service, raise the bound to cover the expected consumers, or disable the dimensions with
    /// <see cref="IncludeConsumerDimensions"/>.
    /// </para>
    /// </remarks>
    public int MaxDistinctValuesPerDimension
    {
        get => _maxDistinctValuesPerDimension;
        set => _maxDistinctValuesPerDimension = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(MaxDistinctValuesPerDimension), value, "Value must be at least 1.")
            : value;
    }

    /// <summary>
    /// Maps a raw claim value to the value recorded on the counter, applying the bound.
    /// </summary>
    internal string Bound(string dimension, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return UnknownValue;

        var seen = _seen.GetOrAdd(dimension, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        if (seen.ContainsKey(value))
            return value;

        if (seen.Count >= _maxDistinctValuesPerDimension)
            return OverflowValue;

        seen.TryAdd(value, 0);
        return value;
    }
}
