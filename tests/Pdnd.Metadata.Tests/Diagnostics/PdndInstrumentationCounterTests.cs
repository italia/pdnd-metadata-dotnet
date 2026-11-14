// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Diagnostics;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Diagnostics;

/// <summary>
/// Covers the request counter dimensions: the client_id fallback and the bound that stops
/// forged voucher claims from creating unbounded time series.
/// </summary>
public sealed class PdndInstrumentationCounterTests
{
    private sealed class CounterCapture : IDisposable
    {
        private readonly MeterListener _listener = new();

        public ConcurrentBag<Dictionary<string, string?>> Measurements { get; } = new();

        public CounterCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == PdndInstrumentation.MeterName &&
                    instrument.Name == PdndInstrumentation.RequestCounterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                var map = new Dictionary<string, string?>();
                foreach (var tag in tags)
                    map[tag.Key] = tag.Value?.ToString();
                Measurements.Add(map);
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static PdndCallerMetadata Voucher(string? consumerId, string purposeId, string? clientIdUnderscore = null)
    {
        var md = new PdndCallerMetadata();
        if (consumerId is not null)
            md.Add(PdndMetadataKeys.PdndVoucherConsumerId, consumerId, PdndMetadataSource.Claims);
        md.Add(PdndMetadataKeys.PdndVoucherPurposeId, purposeId, PdndMetadataSource.Claims);
        if (clientIdUnderscore is not null)
            md.Add(PdndMetadataKeys.PdndVoucherClientIdUnderscore, clientIdUnderscore, PdndMetadataSource.Claims);
        return md;
    }

    private static IEnumerable<Dictionary<string, string?>> ForPurpose(CounterCapture capture, string purposeId)
        => capture.Measurements.Where(m =>
            m.TryGetValue(PdndMetadataKeys.PdndVoucherPurposeId, out var p) && p == purposeId);

    [Fact]
    public void Record_FallsBackToTheUnderscoreClientIdClaim()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();
        var client = "client-" + Guid.NewGuid();

        PdndInstrumentation.Record(Voucher("org", purpose, client), new PdndDiagnosticsOptions());

        ForPurpose(capture, purpose).Should().ContainSingle()
            .Which[PdndMetadataKeys.PdndVoucherClientId].Should().Be(client);
    }

    [Fact]
    public void Record_FallsBackToTheLegacyOrganizationIdClaim()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();

        var md = Voucher(null, purpose);
        md.Add(PdndMetadataKeys.PdndVoucherOrganizationId, "org-legacy", PdndMetadataSource.Claims);

        PdndInstrumentation.Record(md, new PdndDiagnosticsOptions());

        ForPurpose(capture, purpose).Should().ContainSingle()
            .Which[PdndMetadataKeys.PdndVoucherConsumerId].Should().Be("org-legacy");
    }

    [Fact]
    public void Record_ReportsAbsentDimensionsAsUnknown()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();

        PdndInstrumentation.Record(Voucher(null, purpose), new PdndDiagnosticsOptions());

        var measurement = ForPurpose(capture, purpose).Should().ContainSingle().Subject;
        measurement[PdndMetadataKeys.PdndVoucherConsumerId].Should().Be(PdndDiagnosticsOptions.UnknownValue);
        measurement[PdndMetadataKeys.PdndVoucherClientId].Should().Be(PdndDiagnosticsOptions.UnknownValue);
    }

    [Fact]
    public void Record_CollapsesValuesBeyondTheBound_IntoOverflow()
    {
        using var capture = new CounterCapture();
        var options = new PdndDiagnosticsOptions { MaxDistinctValuesPerDimension = 2 };
        var purpose = "purpose-" + Guid.NewGuid();

        PdndInstrumentation.Record(Voucher("org-a", purpose), options);
        PdndInstrumentation.Record(Voucher("org-b", purpose), options);
        PdndInstrumentation.Record(Voucher("org-forged-" + Guid.NewGuid(), purpose), options);
        PdndInstrumentation.Record(Voucher("org-a", purpose), options);

        ForPurpose(capture, purpose)
            .Select(m => m[PdndMetadataKeys.PdndVoucherConsumerId])
            .Should().BeEquivalentTo(new[] { "org-a", "org-b", PdndDiagnosticsOptions.OverflowValue, "org-a" });
    }

    [Fact]
    public void Record_WithoutConsumerDimensions_RecordsNoClaimValues()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();

        PdndInstrumentation.Record(
            Voucher("org", purpose),
            new PdndDiagnosticsOptions { IncludeConsumerDimensions = false });

        ForPurpose(capture, purpose).Should().BeEmpty();
        capture.Measurements.Should().Contain(m => m.Count == 0);
    }

    [Fact]
    public void Record_WithNullOptions_Throws()
    {
        var act = () => PdndInstrumentation.Record(new PdndCallerMetadata(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bound_StaysBounded_UnderConcurrentForgedValues()
    {
        using var capture = new CounterCapture();
        var options = new PdndDiagnosticsOptions { MaxDistinctValuesPerDimension = 10 };
        var purpose = "purpose-" + Guid.NewGuid();

        Parallel.For(0, 2000, i => PdndInstrumentation.Record(Voucher("forged-" + i, purpose), options));

        var distinct = ForPurpose(capture, purpose)
            .Select(m => m[PdndMetadataKeys.PdndVoucherConsumerId])
            .Where(v => v != PdndDiagnosticsOptions.OverflowValue)
            .Distinct()
            .Count();

        // The bound is approximate under contention, by at most one slot per racing thread.
        distinct.Should().BeLessThanOrEqualTo(10 + Environment.ProcessorCount);
    }

    [Fact]
    public async Task UsePdndDiagnostics_WithoutRegisteredOptions_UsesTheDefaults()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();

        // A provider with no PdndDiagnosticsOptions registration at all.
        using var provider = new ServiceCollection().BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UsePdndDiagnostics();
        app.Run(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = Voucher("consumer-1", purpose);

        await app.Build()(context);

        ForPurpose(capture, purpose).Should().ContainSingle()
            .Which[PdndMetadataKeys.PdndVoucherConsumerId].Should().Be("consumer-1");
    }

    [Fact]
    public void AddPdndDiagnostics_WithoutConfigure_RegistersTheDefaults()
    {
        using var provider = new ServiceCollection().AddPdndDiagnostics().BuildServiceProvider();

        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PdndDiagnosticsOptions>>().Value;

        options.IncludeConsumerDimensions.Should().BeTrue();
        options.MaxDistinctValuesPerDimension.Should().Be(100);
    }

    [Fact]
    public void MaxDistinctValuesPerDimension_RejectsValuesBelowOne()
    {
        var act = () => new PdndDiagnosticsOptions { MaxDistinctValuesPerDimension = 0 };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UsePdndDiagnostics_AppliesTheRegisteredOptions()
    {
        using var capture = new CounterCapture();
        var purpose = "purpose-" + Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddPdndDiagnostics(o => o.IncludeConsumerDimensions = false);
        using var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UsePdndDiagnostics();
        app.Run(_ => Task.CompletedTask);
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = Voucher("org", purpose);

        await pipeline(context);

        ForPurpose(capture, purpose).Should().BeEmpty();
    }
}
