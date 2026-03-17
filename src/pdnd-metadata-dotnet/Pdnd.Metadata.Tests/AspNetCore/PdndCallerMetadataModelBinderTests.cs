// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Binding;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.AspNetCore;

public class PdndCallerMetadataModelBinderTests
{
    [Fact]
    public async Task BindModelAsync_ShouldBindMetadata_WhenAccessorReturnsCurrent()
    {
        var md = new PdndCallerMetadata();
        md.Add("test.key", "bound-value", PdndMetadataSource.Derived);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = md;

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IPdndMetadataAccessor, PdndMetadataAccessor>();
        httpContext.RequestServices = services.BuildServiceProvider();

        var binder = new PdndCallerMetadataModelBinder();
        var bindingContext = CreateBindingContext(httpContext);

        await binder.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        var result = bindingContext.Result.Model as PdndCallerMetadata;
        result.Should().NotBeNull();
        result!.GetFirstValue("test.key").Should().Be("bound-value");
    }

    [Fact]
    public async Task BindModelAsync_ShouldReturnNewMetadata_WhenAccessorReturnsNull()
    {
        var httpContext = new DefaultHttpContext();

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IPdndMetadataAccessor, PdndMetadataAccessor>();
        httpContext.RequestServices = services.BuildServiceProvider();

        var binder = new PdndCallerMetadataModelBinder();
        var bindingContext = CreateBindingContext(httpContext);

        await binder.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        var result = bindingContext.Result.Model as PdndCallerMetadata;
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext CreateBindingContext(HttpContext httpContext)
    {
        return new Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext
        {
            ActionContext = new Microsoft.AspNetCore.Mvc.ActionContext
            {
                HttpContext = httpContext
            }
        };
    }
}
