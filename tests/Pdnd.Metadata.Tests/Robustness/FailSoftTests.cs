// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Binding;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Extensions;
using Pdnd.Metadata.AspNetCore.Middleware;
using Pdnd.Metadata.Diagnostics;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;
using Pdnd.Metadata.Tests.TestHelpers;

namespace Pdnd.Metadata.Tests.Robustness;

/// <summary>
/// The library must never fail or block the application that hosts it. Each test pins one way
/// it could, and checks that the request proceeds instead.
/// </summary>
public sealed class FailSoftTests
{
    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger down");
    }

    private sealed class LevelRecorder<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private sealed class ThrowingExtractor : IPdndMetadataExtractor
    {
        public PdndCallerMetadata Extract(PdndRequestContext context, PdndMetadataOptions options)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CustomExtractor : IPdndMetadataExtractor
    {
        public PdndCallerMetadata Extract(PdndRequestContext context, PdndMetadataOptions options) => new();
    }

    private static async Task<bool> RunsNext(Func<RequestDelegate, RequestDelegate> build, HttpContext context)
    {
        var nextCalled = false;
        var pipeline = build(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await pipeline(context);
        return nextCalled;
    }

    // --- Middleware -------------------------------------------------------------------------

    [Fact]
    public async Task Middleware_WhenOptionsCannotBeCreated_ContinuesWithAnEmptySnapshot()
    {
        // A configure delegate that throws surfaces on first access to IOptions.Value.
        var services = new ServiceCollection();
        services.AddPdndMetadata(o => o.MaxHeaderCount = 0);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PdndMetadataOptions>>();
        var context = new DefaultHttpContext();

        var proceeded = await RunsNext(
            next => ctx => new PdndMetadataMiddleware(next).InvokeAsync(ctx, new DefaultPdndMetadataExtractor(), options),
            context);

        proceeded.Should().BeTrue();
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey].Should().BeOfType<PdndCallerMetadata>();
    }

    [Fact]
    public async Task Middleware_WhenTheLoggerThrows_StillProceeds()
    {
        var context = new DefaultHttpContext();

        var proceeded = await RunsNext(
            next => ctx => new PdndMetadataMiddleware(next, new ThrowingLogger<PdndMetadataMiddleware>())
                .InvokeAsync(ctx, new ThrowingExtractor(), Microsoft.Extensions.Options.Options.Create(new PdndMetadataOptions())),
            context);

        proceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_RepeatedFailures_LogOneWarning_ThenDebugEntries()
    {
        var logger = new LevelRecorder<PdndMetadataMiddleware>();
        var middleware = new PdndMetadataMiddleware(_ => Task.CompletedTask, logger);
        var options = Microsoft.Extensions.Options.Options.Create(new PdndMetadataOptions());

        for (var i = 0; i < 5; i++)
            await middleware.InvokeAsync(new DefaultHttpContext(), new ThrowingExtractor(), options);

        logger.Levels.Should().Equal(LogLevel.Warning, LogLevel.Debug, LogLevel.Debug, LogLevel.Debug, LogLevel.Debug);
    }

    [Fact]
    public async Task Middleware_DoesNotSwallowExceptionsFromTheRestOfThePipeline()
    {
        var middleware = new PdndMetadataMiddleware(_ => throw new InvalidOperationException("endpoint"));

        var act = () => middleware.InvokeAsync(
            new DefaultHttpContext(), new DefaultPdndMetadataExtractor(),
            Microsoft.Extensions.Options.Options.Create(new PdndMetadataOptions()));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("endpoint");
    }

    [Fact]
    public async Task UsePdndMetadata_WithoutAddPdndMetadata_RunsWithDefaults()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UsePdndMetadata();
        app.Run(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = "GET";
        context.Request.Headers.Authorization = "Bearer " + Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"consumerId\":\"c-1\"}", "sig");

        await app.Build()(context);

        var snapshot = context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey].Should().BeOfType<PdndCallerMetadata>().Subject;
        snapshot.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().Be("c-1");
    }

    [Fact]
    public async Task UsePdndMetadata_WithAScopedCustomExtractor_StartsUnderScopeValidation()
    {
        // Development turns scope validation on. Resolving the extractor from the root provider
        // to check its registration failed startup for a scoped extractor.
        var services = new ServiceCollection();
        services.AddScoped<IPdndMetadataExtractor, CustomExtractor>();
        services.AddPdndMetadata();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var app = new ApplicationBuilder(provider);
        var act = () => app.UsePdndMetadata();
        act.Should().NotThrow();

        app.Run(_ => Task.CompletedTask);
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        await app.Build()(context);

        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey].Should().BeOfType<PdndCallerMetadata>();
    }

    [Fact]
    public async Task UsePdndMetadata_ResolvesServicesPerRequest_LikeConventionalMiddleware()
    {
        // Multi-tenant containers register services only in the tenant's container, which the
        // request sees through RequestServices. The root provider knows nothing about them.
        using var root = new ServiceCollection().AddLogging().BuildServiceProvider();

        var tenantServices = new ServiceCollection();
        tenantServices.AddSingleton<IPdndMetadataExtractor, CustomExtractor>();
        tenantServices.AddPdndMetadata(o => o.PathExclusions.Add("/excluded"));
        using var tenant = tenantServices.BuildServiceProvider();

        var app = new ApplicationBuilder(root);
        app.UsePdndMetadata();
        app.Run(_ => Task.CompletedTask);
        var pipeline = app.Build();

        var excluded = new DefaultHttpContext { RequestServices = tenant };
        excluded.Request.Path = "/excluded/x";
        await pipeline(excluded);
        excluded.Items.ContainsKey(PdndMetadataAspNetCoreConstants.HttpContextItemKey)
            .Should().BeFalse("the tenant's options must apply");

        var included = new DefaultHttpContext { RequestServices = tenant };
        included.Request.Path = "/api";
        included.Request.Headers.Authorization = "Bearer " + Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"consumerId\":\"c-1\"}", "sig");
        await pipeline(included);

        // CustomExtractor returns an empty snapshot: the tenant's extractor was used, not the default.
        included.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey].Should().BeOfType<PdndCallerMetadata>()
            .Which.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId).Should().BeNull();
    }

    [Fact]
    public async Task UsePdndMetadata_WhenTheRegisteredExtractorFailsToResolve_DoesNotFallBackToDefaults()
    {
        // Falling back to the default extractor and default options would ignore the operator's
        // exclusions and capture restrictions. The request proceeds with an empty snapshot.
        var logger = new LevelRecorder<PdndMetadataMiddleware>();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger));
        services.AddSingleton<IPdndMetadataExtractor>(_ => throw new InvalidOperationException("broken factory"));
        services.AddPdndMetadata(o => o.HeaderAllowList.Remove("User-Agent"));
        using var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UsePdndMetadata();
        var nextCalled = false;
        app.Run(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.UserAgent = "client/1.0";
        context.Request.Headers.Authorization = "Bearer " + Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"consumerId\":\"c-1\"}", "sig");

        await app.Build()(context);

        nextCalled.Should().BeTrue();
        var snapshot = context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey].Should().BeOfType<PdndCallerMetadata>().Subject;
        snapshot.Snapshot().Should().BeEmpty("nothing must be extracted under configuration that was not applied");
        logger.Levels.Should().Contain(LogLevel.Warning);
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;
        public SingleLoggerFactory(ILogger logger) => _logger = logger;
        public ILogger CreateLogger(string categoryName) => _logger;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    [Fact]
    public void AddPdndMetadata_IsIdempotent_AndKeepsACustomExtractor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPdndMetadataExtractor, CustomExtractor>();
        services.AddPdndMetadata();
        services.AddPdndMetadata();

        services.Count(d => d.ServiceType == typeof(IPdndMetadataExtractor)).Should().Be(1);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPdndMetadataExtractor>().Should().BeOfType<CustomExtractor>();
    }

    // --- Binding without registration -------------------------------------------------------

    [Fact]
    public async Task MinimalApiBinding_WithoutAddPdndMetadata_ReadsTheSnapshotFromItems()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        var snapshot = new PdndCallerMetadata();
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = snapshot;

        var parameter = typeof(FailSoftTests).GetMethod(nameof(Endpoint), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters()[0];

        var bound = await PdndCallerMetadataParameter.BindAsync(context, parameter);

        bound.Value.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task ModelBinding_WithoutAddPdndMetadata_ReturnsAnEmptySnapshot()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var binding = new DefaultModelBindingContext
        {
            ActionContext = new Microsoft.AspNetCore.Mvc.ActionContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = provider }
            }
        };

        await new PdndCallerMetadataModelBinder().BindModelAsync(binding);

        binding.Result.IsModelSet.Should().BeTrue();
        binding.Result.Model.Should().BeOfType<PdndCallerMetadata>();
    }

    private static void Endpoint(PdndCallerMetadataParameter pdnd) => _ = pdnd;

    // --- Diagnostics ------------------------------------------------------------------------

    [Fact]
    public async Task DiagnosticsMiddleware_WhenAMetricListenerThrows_StillProceeds()
    {
        // Meter listeners are process-wide and xUnit runs test classes in parallel. A listener
        // that threw on every measurement would break any other test recording at the same time,
        // and would stop later listeners from seeing their measurements. It throws only for the
        // measurement this test produces, recognised by a unique purpose.
        var marker = "purpose-" + Guid.NewGuid();
        var thrown = 0;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == PdndInstrumentation.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == PdndMetadataKeys.PdndVoucherPurposeId && Equals(tag.Value, marker))
                {
                    Interlocked.Increment(ref thrown);
                    throw new InvalidOperationException("exporter down");
                }
            }
        });
        listener.Start();

        var metadata = new PdndCallerMetadata();
        metadata.Add(PdndMetadataKeys.PdndVoucherPurposeId, marker, PdndMetadataSource.Claims);

        var context = new DefaultHttpContext();
        context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = metadata;

        var proceeded = await RunsNext(next => new PdndDiagnosticsMiddleware(next).InvokeAsync, context);

        thrown.Should().Be(1, "the listener must actually have failed for the test to mean anything");
        proceeded.Should().BeTrue();
    }

    // --- Extraction under hostile input -----------------------------------------------------

    [Fact]
    public void Extract_NeverThrows_OnRandomisedHeaderValues()
    {
        // Fixed seed: reproducible, but covers dots, Base64Url, Base64, JSON fragments, control
        // characters, and lone surrogates in every PDND header.
        var random = new Random(20261114);
        const string alphabet = "ABCabc012-_+/=.{}\":,[] \t\u0000𐀀é";
        string NextValue()
        {
            var length = random.Next(0, 400);
            var builder = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                builder.Append(alphabet[random.Next(alphabet.Length)]);
            return builder.ToString();
        }

        var names = new[] { "Authorization", "DPoP", "Agid-JWT-TrackingEvidence", "Agid-JWT-Signature",
                            "Digest", "Content-Digest", "Forwarded", "X-Forwarded-For", "User-Agent" };
        var extractor = new DefaultPdndMetadataExtractor();
        var options = new PdndMetadataOptions { CaptureAllHeaders = true };

        for (var iteration = 0; iteration < 2000; iteration++)
        {
            var context = new PdndRequestContext
            {
                Method = "POST",
                Path = NextValue(),
                Headers = names.Select(n => new PdndRequestHeader(n,
                    new[] { (n == "Authorization" ? "Bearer " : "") + NextValue(), NextValue() })).ToArray()
            };

            var act = () => extractor.Extract(context, options);
            act.Should().NotThrow($"iteration {iteration}");
        }
    }

    [Fact]
    public void Extract_IsSafeForConcurrentRequests_OnASingleExtractor()
    {
        var extractor = new DefaultPdndMetadataExtractor();
        var options = new PdndMetadataOptions();
        var failures = 0;

        Parallel.For(0, 2000, i =>
        {
            var voucher = Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"consumerId\":\"c-" + i + "\"}", "sig");
            var md = extractor.Extract(new PdndRequestContext
            {
                Method = "GET",
                Headers = new[] { new PdndRequestHeader("Authorization", new[] { "Bearer " + voucher }) }
            }, options);

            if (md.GetFirstValue(PdndMetadataKeys.PdndVoucherConsumerId) != "c-" + i)
                Interlocked.Increment(ref failures);
        });

        failures.Should().Be(0, "the extractor is a singleton and must not share state between requests");
    }

    [Fact]
    public void Extract_DoesNotMutateTheCallersHeaderLists()
    {
        var first = new List<string> { "for=1.1.1.1" };
        var second = new List<string> { "for=2.2.2.2" };

        new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "GET",
            Headers = new[] { new PdndRequestHeader("Forwarded", first), new PdndRequestHeader("forwarded", second) }
        }, new PdndMetadataOptions());

        first.Should().Equal("for=1.1.1.1");
        second.Should().Equal("for=2.2.2.2");
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("Proxy-Authorization")]
    [InlineData("cookie")]
    public void SignedCredentialHeaders_AreNeverStored(string name)
    {
        var voucher = Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"purposeId\":\"p\"}", "sig");
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"signed_headers\":[{\"digest\":\"SHA-256=a=\"},{\"" + name + "\":\"Bearer " + voucher + "\"}]}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "POST",
            Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) }
        }, new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "digest").Should().Be("SHA-256=a=");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + name.ToLowerInvariant()).Should().BeNull();
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().Contain(name.ToLowerInvariant(),
            "the name stays listed; only the value is withheld");
        md.Snapshot().Values.SelectMany(items => items).Should().NotContain(i => i.Value.Contains(voucher));
    }

    [Theory]
    [InlineData(" authorization")]
    [InlineData("authorization ")]
    [InlineData("\tAuthorization")]
    [InlineData(" x-api-key")]
    public void SignedCredentialHeaders_CannotBeSmuggledWithPadding(string paddedName)
    {
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"signed_headers\":[{\"" + paddedName.Replace("\t", "\\t") + "\":\"secret-credential\"}]}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "POST",
            Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) }
        }, new PdndMetadataOptions());

        md.Snapshot().Values.SelectMany(items => items).Should().NotContain(i => i.Value == "secret-credential");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaders).Should().Be(paddedName.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("\\u200Bauthorization")]   // zero-width space, not removed by Trim()
    [InlineData("authorization\\u200B")]
    [InlineData("\\uFEFFauthorization")]   // byte order mark
    [InlineData("x-api-key\\u200B")]
    [InlineData("auth orization")]         // inner space: not a header name either
    public void SignedHeaderNames_ThatAreNotHttpTokens_DoNotBecomeKeys(string jsonEscapedName)
    {
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}",
            "{\"signed_headers\":[{\"digest\":\"SHA-256=a=\"},{\"" + jsonEscapedName + "\":\"secret-credential\"}]}",
            "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "POST",
            Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) }
        }, new PdndMetadataOptions());

        md.Snapshot().Values.SelectMany(items => items).Should().NotContain(i => i.Value == "secret-credential");
        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "digest").Should().Be("SHA-256=a=");
    }

    [Fact]
    public void SignedHeaders_UnderAConfiguredDenyListName_AreNotStored()
    {
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"signed_headers\":[{\"x-api-key\":\"secret-key\"}]}", "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "POST",
            Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) }
        }, new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + "x-api-key").Should().BeNull();
    }

    [Fact]
    public void SignedHeaderNames_BeyondTheBound_DoNotBecomeKeys()
    {
        var longName = new string('x', 300);
        var signature = Base64UrlTestHelper.Jwt(
            "{\"alg\":\"RS256\"}", "{\"signed_headers\":[{\"" + longName + "\":\"v\"}]}", "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "POST",
            Headers = new[] { new PdndRequestHeader("Agid-JWT-Signature", new[] { signature }) }
        }, new PdndMetadataOptions());

        md.Snapshot().Keys.Should().NotContain(k => k.Length > 300);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    public void AnUnusableHyphenatedHeader_DoesNotHideTheOfficialTrackingEvidence(string hyphenated)
    {
        var evidence = Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"jti\":\"te-1\"}", "sig");

        var md = new DefaultPdndMetadataExtractor().Extract(new PdndRequestContext
        {
            Method = "GET",
            Headers = new[]
            {
                new PdndRequestHeader("Agid-JWT-Tracking-Evidence", new[] { hyphenated }),
                new PdndRequestHeader("Agid-JWT-TrackingEvidence", new[] { evidence })
            }
        }, new PdndMetadataOptions());

        md.GetFirstValue(PdndMetadataKeys.PdndTrackingJti).Should().Be("te-1");
    }

    [Fact]
    public void Truncation_NeverSplitsASurrogatePair()
    {
        // "aaa" followed by an emoji (a surrogate pair): a bound of 4 falls between its halves.
        var context = new PdndRequestContext { Method = "GET", Path = "aaa\U0001F600b" };

        var path = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { MaxValueLength = 4 })
            .GetFirstValue(PdndMetadataKeys.HttpPath);

        path.Should().Be("aaa");
    }

    [Fact]
    public void PathAndHost_AreBoundedByMaxValueLength()
    {
        var context = new PdndRequestContext { Method = "GET", Path = "/" + new string('p', 100), Host = new string('h', 100) };

        var md = new DefaultPdndMetadataExtractor().Extract(context, new PdndMetadataOptions { MaxValueLength = 10 });

        md.GetFirstValue(PdndMetadataKeys.HttpPath).Should().HaveLength(10);
        md.GetFirstValue(PdndMetadataKeys.HttpHost).Should().HaveLength(10);
    }

    // --- JWT decoding without exceptions -----------------------------------------------------

    [Fact]
    public void JwtDecoder_DecodesTokensLargerThanTheStackBuffer()
    {
        // A payload well above 1024 characters takes the pooled-buffer path.
        var big = new string('x', 5000);
        var token = Base64UrlTestHelper.Jwt("{\"alg\":\"RS256\"}", "{\"jti\":\"" + big + "\"}", "sig");

        JwtDecoder.TryDecode(token, out var parts).Should().BeTrue();
        parts!.PayloadJson.Should().Contain(big);
    }

    [Theory]
    [InlineData("e30.e30xx")]       // payload length 4n+1 cannot be Base64
    [InlineData("e30.e3+0")]        // standard Base64 character
    [InlineData("e30.e3 0")]        // whitespace, tolerated by Convert but not by Base64Url
    [InlineData("e30.wyg")]         // decodes to 0xC3 0x28: invalid UTF-8
    public void JwtDecoder_RejectsMalformedSegments(string token)
    {
        JwtDecoder.TryDecode(token, out var parts).Should().BeFalse();
        parts.Should().BeNull();
    }
}
