// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Diagnostics;

/// <summary>
/// Projects a <see cref="PdndCallerMetadata"/> snapshot onto the ambient <see cref="Activity"/>
/// and onto a request counter.
/// </summary>
/// <remarks>
/// This type has no dependency on any OpenTelemetry package. It writes to
/// <see cref="Activity"/> and <see cref="Meter"/> from the base class library, which the
/// OpenTelemetry SDK consumes natively once the source names below are subscribed. A consumer
/// that does not use OpenTelemetry pays nothing beyond a null check, because
/// <see cref="Activity.Current"/> is null when no listener is registered.
/// </remarks>
public static class PdndInstrumentation
{
    /// <summary>
    /// Reserved name for an activity source. No <see cref="ActivitySource"/> is created under it:
    /// tags are written to <see cref="Activity.Current"/>, the request activity created by
    /// ASP.NET Core, so subscribing to this name has no effect today.
    /// </summary>
    public const string ActivitySourceName = "Pdnd.Metadata";

    /// <summary>Name of the meter, to be passed to <c>AddMeter</c>.</summary>
    public const string MeterName = "Pdnd.Metadata";

    /// <summary>Name of the request counter.</summary>
    public const string RequestCounterName = "pdnd.requests";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        name: RequestCounterName,
        unit: "{request}",
        description: "Inbound requests grouped by PDND consumer identity.");

    // Used by the overload without options. Its distinct-value bound is process-wide.
    private static readonly PdndDiagnosticsOptions DefaultOptions = new();

    /// <summary>
    /// Canonical metadata keys promoted to activity tags.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow. Standard HTTP attributes are left to the ASP.NET Core
    /// instrumentation, which already emits them under the OpenTelemetry semantic conventions;
    /// duplicating them here under <c>pdnd.*</c> names would create two spellings for one fact.
    /// Only PDND-specific identity and integrity signals are promoted. Nothing here carries a
    /// credential: the voucher itself is never a tag value, only claims about it are.
    /// </remarks>
    private static readonly string[] PromotedKeys =
    [
        PdndMetadataKeys.PdndVoucherIss,
        PdndMetadataKeys.PdndVoucherSub,
        PdndMetadataKeys.PdndVoucherPurposeId,
        PdndMetadataKeys.PdndVoucherClientId,
        PdndMetadataKeys.PdndVoucherClientIdUnderscore,
        PdndMetadataKeys.PdndVoucherConsumerId,
        PdndMetadataKeys.PdndVoucherOrganizationId,
        PdndMetadataKeys.PdndVoucherEserviceId,
        PdndMetadataKeys.PdndVoucherDescriptorId,
        PdndMetadataKeys.PdndVoucherJti,
        PdndMetadataKeys.PdndTrackingJti,
        PdndMetadataKeys.PdndSignatureKid,
        PdndMetadataKeys.PdndSignatureSignedHeaders,
        PdndMetadataKeys.PdndDpopHtm,
        PdndMetadataKeys.PdndDigestAlg,
        PdndMetadataKeys.PdndContentDigestAlg
    ];

    /// <summary>
    /// Writes the promoted metadata onto the current activity and records one counter
    /// measurement dimensioned by consumer identity, using process-wide default options.
    /// </summary>
    /// <param name="metadata">Snapshot produced by the extraction pipeline. May be <c>null</c>.</param>
    public static void Record(PdndCallerMetadata? metadata)
        => Record(metadata, DefaultOptions);

    /// <summary>
    /// Writes the promoted metadata onto the current activity and records one counter
    /// measurement, dimensioned according to <paramref name="options"/>.
    /// </summary>
    /// <param name="metadata">Snapshot produced by the extraction pipeline. May be <c>null</c>.</param>
    /// <param name="options">
    /// Counter dimensioning options. Reuse one instance across requests: it holds the
    /// distinct-value bound.
    /// </param>
    public static void Record(PdndCallerMetadata? metadata, PdndDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (metadata is null)
            return;

        var activity = Activity.Current;

        if (activity is not null)
        {
            foreach (var key in PromotedKeys)
            {
                var value = metadata.GetFirstValue(key);
                if (!string.IsNullOrEmpty(value))
                    activity.SetTag(key, value);
            }

            // Truncation is a property of the snapshot, not of the caller, but it changes how
            // every other tag should be read, so it is always propagated when present.
            var truncated = metadata.GetFirstValue(PdndMetadataKeys.MetadataTruncated);
            if (!string.IsNullOrEmpty(truncated))
                activity.SetTag(PdndMetadataKeys.MetadataTruncated, truncated);
        }

        if (!options.IncludeConsumerDimensions)
        {
            RequestCounter.Add(1);
            return;
        }

        // Counter dimensions are kept to three low-cardinality claims, each bounded because the
        // claims come from an unverified token. Adding jti here would make every request a
        // distinct time series.
        //
        // PDND vouchers carry the OAuth 2.0 "client_id" claim. The camelCase form is accepted
        // too, so the dimension falls back from one to the other.
        var clientId = metadata.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId)
                       ?? metadata.GetFirstValue(PdndMetadataKeys.PdndVoucherClientIdUnderscore);

        // Current PDND vouchers identify the fruitore with "consumerId"; "organizationId" is
        // read only as a fallback for vouchers that still carry it.
        var consumerId = metadata.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId)
                         ?? metadata.GetFirstValue(PdndMetadataKeys.PdndVoucherOrganizationId);

        RequestCounter.Add(
            1,
            new KeyValuePair<string, object?>(
                PdndMetadataKeys.PdndVoucherConsumerId,
                options.Bound(PdndMetadataKeys.PdndVoucherConsumerId, consumerId)),
            new KeyValuePair<string, object?>(
                PdndMetadataKeys.PdndVoucherPurposeId,
                options.Bound(
                    PdndMetadataKeys.PdndVoucherPurposeId,
                    metadata.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId))),
            new KeyValuePair<string, object?>(
                PdndMetadataKeys.PdndVoucherClientId,
                options.Bound(PdndMetadataKeys.PdndVoucherClientId, clientId)));
    }
}
