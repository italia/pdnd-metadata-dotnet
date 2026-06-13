// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Connections.Features;
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

    [Fact]
    public void Map_ShouldExposeMtlsClientCertificateThumbprint_WhenClientCertificatePresent()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        using var cert = CreateSelfSignedCertificate();
        httpContext.Features.Set<ITlsConnectionFeature>(new TestTlsConnectionFeature(cert));

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.SecurityHints.Should().ContainKey("mtls.client_certificate_present")
            .WhoseValue.Should().Be("true");
        ctx.SecurityHints.Should().ContainKey("mtls.client_certificate_thumbprint")
            .WhoseValue.Should().Be(cert.Thumbprint);
    }

    [Fact]
    public void Map_ShouldNotExposeMtlsHints_WhenNoClientCertificate()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Features.Set<ITlsConnectionFeature>(new TestTlsConnectionFeature(clientCertificate: null));

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.SecurityHints.Should().NotContainKey("mtls.client_certificate_present");
        ctx.SecurityHints.Should().NotContainKey("mtls.client_certificate_thumbprint");
    }

    [Fact]
    public void Map_ShouldExposeTlsProtocol_WhenHandshakeFeatureAvailable()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Features.Set<ITlsHandshakeFeature>(new TestTlsHandshakeFeature(SslProtocols.Tls12));

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.SecurityHints.Should().ContainKey("tls.protocol")
            .WhoseValue.Should().Be(SslProtocols.Tls12.ToString());
    }

    [Fact]
    public void Map_ShouldNotExposeTlsProtocol_WhenHandshakeReportsNone()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Features.Set<ITlsHandshakeFeature>(new TestTlsHandshakeFeature(SslProtocols.None));

        var ctx = HttpContextPdndRequestContextMapper.Map(httpContext);

        ctx.SecurityHints.Should().NotContainKey("tls.protocol");
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=pdnd-metadata-tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddHours(1);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private sealed class TestTlsConnectionFeature : ITlsConnectionFeature
    {
        public TestTlsConnectionFeature(X509Certificate2? clientCertificate)
        {
            ClientCertificate = clientCertificate;
        }

        public X509Certificate2? ClientCertificate { get; set; }

        public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken)
            => Task.FromResult(ClientCertificate);
    }

    private sealed class TestTlsHandshakeFeature : ITlsHandshakeFeature
    {
        public TestTlsHandshakeFeature(SslProtocols protocol)
        {
            Protocol = protocol;
        }

        public SslProtocols Protocol { get; }

#pragma warning disable SYSLIB0058 // Obsolete TLS handshake properties are still required by the interface contract.
        public CipherAlgorithmType CipherAlgorithm => CipherAlgorithmType.None;

        public int CipherStrength => 0;

        public HashAlgorithmType HashAlgorithm => HashAlgorithmType.None;

        public int HashStrength => 0;

        public ExchangeAlgorithmType KeyExchangeAlgorithm => ExchangeAlgorithmType.None;

        public int KeyExchangeStrength => 0;
#pragma warning restore SYSLIB0058
    }
}
