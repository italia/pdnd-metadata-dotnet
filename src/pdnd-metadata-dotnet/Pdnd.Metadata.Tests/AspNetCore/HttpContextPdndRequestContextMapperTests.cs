// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Pdnd.Metadata.AspNetCore.Mapping;
using Pdnd.Metadata.Extraction;

namespace Pdnd.Metadata.Tests.AspNetCore;

public class HttpContextPdndRequestContextMapperTests
{
    [Fact]
    public void Map_ShouldMapBasicRequestProperties()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        httpContext.Request.Path = "/v1/resource";
        httpContext.Request.QueryString = new QueryString("?id=42");
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        httpContext.Connection.RemotePort = 54321;
        httpContext.Connection.LocalIpAddress = IPAddress.Parse("10.0.0.1");
        httpContext.Connection.LocalPort = 443;

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.Method.Should().Be("POST");
        ctx.Scheme.Should().Be("https");
        ctx.Host.Should().Be("api.example.com");
        ctx.Path.Should().Be("/v1/resource");
        ctx.QueryString.Should().Be("?id=42");
        ctx.RemoteIpAddress.Should().Be(IPAddress.Parse("192.168.1.1"));
        ctx.RemotePort.Should().Be(54321);
        ctx.LocalIpAddress.Should().Be(IPAddress.Parse("10.0.0.1"));
        ctx.LocalPort.Should().Be(443);
    }

    [Fact]
    public void Map_ShouldCaptureHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers["X-Custom"] = "value1";
        httpContext.Request.Headers["Accept"] = "application/json";

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.Headers.Should().Contain(h => h.Name == "X-Custom");
        ctx.Headers.Should().Contain(h => h.Name == "Accept");
    }

    [Fact]
    public void Map_ShouldPromoteTracingHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers["traceparent"] = "00-abcdef1234567890abcdef1234567890-1234567890abcdef-01";
        httpContext.Request.Headers["tracestate"] = "vendor=value";
        httpContext.Request.Headers["x-correlation-id"] = "corr-123";
        httpContext.Request.Headers["x-request-id"] = "req-456";

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.TracingHints.Should().ContainKey("traceparent");
        ctx.TracingHints.Should().ContainKey("tracestate");
        ctx.TracingHints.Should().ContainKey("x-correlation-id");
        ctx.TracingHints.Should().ContainKey("x-request-id");
    }

    [Fact]
    public void Map_ShouldSetHttpsSecurityHint_WhenSchemeIsHttps()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "https";
        httpContext.Request.IsHttps = true;

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.SecurityHints.Should().ContainKey("https")
            .WhoseValue.Should().Be("true");
    }

    [Fact]
    public void Map_ShouldReturnNullPort_WhenPortIsZero()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Connection.RemotePort = 0;
        httpContext.Connection.LocalPort = 0;

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.RemotePort.Should().BeNull();
        ctx.LocalPort.Should().BeNull();
    }

    [Fact]
    public void Map_ShouldReturnEmptyClaims_WhenNoPrincipal()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.Claims.Should().BeEmpty();
    }
}
