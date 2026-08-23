// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Diagnostics;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Diagnostics;

/// <summary>
/// Covers the contract between the two middleware components: one writes the snapshot into
/// <c>HttpContext.Items</c>, the other reads it back.
/// </summary>
/// <remarks>
/// The diagnostics middleware originally carried its own copy of the item key, which did not
/// match the one the metadata middleware writes. Nothing failed: instrumentation was simply
/// never recorded. These tests exist so that the two cannot drift apart again silently.
/// </remarks>
public sealed class PdndDiagnosticsMiddlewareTests
{
    private static ActivityListener CreateListener(List<Activity> collected) => new()
    {
        ShouldListenTo = source => source.Name == PdndInstrumentation.ActivitySourceName || source.Name == "test",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStopped = collected.Add
    };

    [Fact]
    public async Task InvokeAsync_ReadsTheKeyTheMetadataMiddlewareWrites()
    {
        var metadata = new PdndCallerMetadata();
        metadata.Add(PdndMetadataKeys.PdndVoucherOrganizationId, "org-001", PdndMetadataSource.Claims);

        var context = new DefaultHttpContext();
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = metadata;

        var collected = new List<Activity>();
        using var listener = CreateListener(collected);
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("test");
        using var activity = source.StartActivity("request");

        var nextWasCalled = false;
        var middleware = new PdndDiagnosticsMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextWasCalled.Should().BeTrue();
        activity.Should().NotBeNull();
        activity!.GetTagItem(PdndMetadataKeys.PdndVoucherOrganizationId).Should().Be("org-001");
    }

    [Fact]
    public async Task InvokeAsync_WithNoSnapshot_CallsNextWithoutRecording()
    {
        var context = new DefaultHttpContext();

        var nextWasCalled = false;
        var middleware = new PdndDiagnosticsMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithUnexpectedItemType_DoesNotThrow()
    {
        var context = new DefaultHttpContext();
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = "not-metadata";

        var middleware = new PdndDiagnosticsMiddleware(_ => Task.CompletedTask);

        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Record_WithNullMetadata_DoesNotThrow()
    {
        var act = () => PdndInstrumentation.Record(null);

        act.Should().NotThrow();
    }
}
