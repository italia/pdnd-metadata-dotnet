// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Middleware;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Pdnd.Metadata.Tests.AspNetCore;

public class PdndMetadataMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldStoreMetadataInHttpContextItems()
    {
        var extractor = new DefaultPdndMetadataExtractor();
        var options = MsOptions.Create(new PdndMetadataOptions());
        var middleware = new PdndMetadataMiddleware(_ => Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("test.example.com");

        await middleware.InvokeAsync(httpContext, extractor, options);

        httpContext.Items.Should().ContainKey(PdndMetadataAspNetCoreConstants.HttpContextItemKey);
        var md = httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] as PdndCallerMetadata;
        md.Should().NotBeNull();
        md!.GetFirstValue(PdndMetadataKeys.HttpMethod).Should().Be("GET");
        md.GetFirstValue(PdndMetadataKeys.HttpScheme).Should().Be("https");
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotOverwriteExistingMetadata()
    {
        var extractor = new DefaultPdndMetadataExtractor();
        var options = MsOptions.Create(new PdndMetadataOptions());
        var middleware = new PdndMetadataMiddleware(_ => Task.CompletedTask);

        var existingMd = new PdndCallerMetadata();
        existingMd.Add("test.key", "original", PdndMetadataSource.Derived);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = existingMd;

        await middleware.InvokeAsync(httpContext, extractor, options);

        var md = httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] as PdndCallerMetadata;
        md.Should().BeSameAs(existingMd);
        md!.GetFirstValue("test.key").Should().Be("original");
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        var extractor = new DefaultPdndMetadataExtractor();
        var options = MsOptions.Create(new PdndMetadataOptions());
        var nextCalled = false;
        var middleware = new PdndMetadataMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        await middleware.InvokeAsync(httpContext, extractor, options);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldExtractPdndHeaders_WhenPresent()
    {
        var extractor = new DefaultPdndMetadataExtractor();
        var opts = new PdndMetadataOptions
        {
            ParseDigestHeader = true
        };
        var options = MsOptions.Create(opts);
        var middleware = new PdndMetadataMiddleware(_ => Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers["Digest"] = "SHA-256=abc123";

        await middleware.InvokeAsync(httpContext, extractor, options);

        var md = httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] as PdndCallerMetadata;
        md.Should().NotBeNull();
        md!.GetFirstValue(PdndMetadataKeys.PdndDigestAlg).Should().Be("SHA-256");
        md.GetFirstValue(PdndMetadataKeys.PdndDigestValue).Should().Be("abc123");
    }
}
