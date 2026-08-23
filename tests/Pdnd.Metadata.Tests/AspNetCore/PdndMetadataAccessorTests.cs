// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.AspNetCore;

public class PdndMetadataAccessorTests
{
    [Fact]
    public void Current_ShouldReturnNull_WhenNoHttpContext()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var accessor = new PdndMetadataAccessor(httpContextAccessor);

        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void Current_ShouldReturnNull_WhenNoMetadataInItems()
    {
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var accessor = new PdndMetadataAccessor(httpContextAccessor);

        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void Current_ShouldReturnMetadata_WhenPresentInItems()
    {
        var md = new PdndCallerMetadata();
        md.Add("test.key", "test-value", PdndMetadataSource.Derived);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = md;

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var accessor = new PdndMetadataAccessor(httpContextAccessor);

        var result = accessor.Current;
        result.Should().NotBeNull();
        result.Should().BeSameAs(md);
        result!.GetFirstValue("test.key").Should().Be("test-value");
    }

    [Fact]
    public void Current_ShouldReturnNull_WhenItemIsNotPdndCallerMetadata()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = "not-metadata";

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var accessor = new PdndMetadataAccessor(httpContextAccessor);

        accessor.Current.Should().BeNull();
    }
}
