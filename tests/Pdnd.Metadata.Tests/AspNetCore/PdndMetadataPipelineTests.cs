// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Binding;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Extensions;
using Pdnd.Metadata.AspNetCore.Middleware;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Pdnd.Metadata.Tests.AspNetCore;

/// <summary>
/// Covers the ASP.NET Core integration as an application registers it: the middleware's skip and
/// fail-soft paths, the Minimal API and MVC binding entry points, and the whole pipeline wired
/// through <c>AddPdndMetadata()</c> and <c>UsePdndMetadata()</c>.
/// </summary>
public sealed class PdndMetadataPipelineTests
{
    private sealed class ThrowingExtractor : IPdndMetadataExtractor
    {
        public PdndCallerMetadata Extract(PdndRequestContext context, PdndMetadataOptions options)
            => throw new InvalidOperationException("boom");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }

    private static object? Snapshot(HttpContext context)
        => context.Items.TryGetValue(PdndMetadataAspNetCoreConstants.HttpContextItemKey, out var value) ? value : null;

    [Theory]
    [InlineData("/health", false)]
    [InlineData("/health/ready", false)]
    [InlineData("/healthcare", true)]
    public async Task InvokeAsync_SkipsExtraction_ForExcludedPaths_ButAlwaysCallsNext(string path, bool expectSnapshot)
    {
        var options = new PdndMetadataOptions();
        options.PathExclusions.Add("/health");

        var nextCalled = false;
        var middleware = new PdndMetadataMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;

        await middleware.InvokeAsync(context, new DefaultPdndMetadataExtractor(), MsOptions.Create(options));

        nextCalled.Should().BeTrue();
        (Snapshot(context) is not null).Should().Be(expectSnapshot);
    }

    [Fact]
    public async Task InvokeAsync_WhenExtractionThrows_StoresAnEmptySnapshot_LogsAWarning_AndContinues()
    {
        var logger = new RecordingLogger<PdndMetadataMiddleware>();
        var nextCalled = false;
        var middleware = new PdndMetadataMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";

        await middleware.InvokeAsync(context, new ThrowingExtractor(), MsOptions.Create(new PdndMetadataOptions()));

        nextCalled.Should().BeTrue("a failed extraction must never block the request");
        var snapshot = Snapshot(context).Should().BeOfType<PdndCallerMetadata>().Subject;
        snapshot.Snapshot().Should().BeEmpty();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task InvokeAsync_WhenExtractionThrows_WithoutALogger_DoesNotThrow()
    {
        var middleware = new PdndMetadataMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        var act = () => middleware.InvokeAsync(context, new ThrowingExtractor(), MsOptions.Create(new PdndMetadataOptions()));

        await act.Should().NotThrowAsync();
        Snapshot(context).Should().BeOfType<PdndCallerMetadata>();
    }

    [Fact]
    public async Task UsePdndMetadata_ExtractsTheVoucher_AndExposesItThroughTheAccessor()
    {
        var services = new ServiceCollection();
        services.AddPdndMetadata();
        using var provider = services.BuildServiceProvider();

        PdndCallerMetadata? seen = null;
        var app = new ApplicationBuilder(provider);
        app.UsePdndMetadata();
        app.Run(ctx =>
        {
            seen = ctx.RequestServices.GetRequiredService<IPdndMetadataAccessor>().Current;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        context.Request.Method = "GET";
        context.Request.Headers.Authorization = "DPoP " + Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"consumerId\":\"consumer-1\",\"purposeId\":\"purpose-1\"}", "sig");

        await pipeline(context);

        seen.Should().NotBeNull();
        seen!.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().Be("consumer-1");
        seen.GetFirstValue(PdndMetadataKeys.HeaderPrefix + "authorization").Should().BeNull();
    }

    [Fact]
    public async Task MinimalApiParameter_BindsTheCurrentSnapshot()
    {
        var services = new ServiceCollection();
        services.AddPdndMetadata();
        using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        var snapshot = new PdndCallerMetadata();
        snapshot.Add(PdndMetadataKeys.PdndVoucherPurposeId, "purpose-1", PdndMetadataSource.Claims);
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = snapshot;

        var parameter = typeof(PdndMetadataPipelineTests)
            .GetMethod(nameof(Endpoint), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        var bound = await PdndCallerMetadataParameter.BindAsync(context, parameter);

        bound.Value.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task MinimalApiParameter_WithoutASnapshot_BindsAnEmptyOne()
    {
        var services = new ServiceCollection();
        services.AddPdndMetadata();
        using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        var parameter = typeof(PdndMetadataPipelineTests)
            .GetMethod(nameof(Endpoint), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        var bound = await PdndCallerMetadataParameter.BindAsync(context, parameter);

        bound.Value.Should().NotBeNull();
        bound.Value.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void FromPdndMetadataAttribute_SelectsTheMetadataModelBinder()
    {
        new FromPdndMetadataAttribute().BinderType.Should().Be(typeof(PdndCallerMetadataModelBinder));
    }

    // Only its parameter is used, as the ParameterInfo a Minimal API passes to BindAsync.
    private static void Endpoint(PdndCallerMetadataParameter pdnd) => _ = pdnd;
}
