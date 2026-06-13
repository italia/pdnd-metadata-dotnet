// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Extensions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Options;

namespace Pdnd.Metadata.Tests.AspNetCore;

public class PdndMetadataServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPdndMetadata_ShouldRegisterRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddPdndMetadata();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IHttpContextAccessor>().Should().NotBeNull();
        provider.GetService<IPdndMetadataExtractor>().Should().NotBeNull();
        provider.GetService<IOptions<PdndMetadataOptions>>().Should().NotBeNull();
    }

    [Fact]
    public void AddPdndMetadata_ShouldRegisterExtractorAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddPdndMetadata();

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IPdndMetadataExtractor>();
        var second = provider.GetRequiredService<IPdndMetadataExtractor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddPdndMetadata_ShouldRegisterAccessorAsScoped()
    {
        var services = new ServiceCollection();
        services.AddPdndMetadata();

        using var provider = services.BuildServiceProvider();

        IPdndMetadataAccessor accessorFromFirstScope;
        IPdndMetadataAccessor sameInstanceFromFirstScope;
        IPdndMetadataAccessor accessorFromSecondScope;

        using (var scope = provider.CreateScope())
        {
            accessorFromFirstScope = scope.ServiceProvider.GetRequiredService<IPdndMetadataAccessor>();
            sameInstanceFromFirstScope = scope.ServiceProvider.GetRequiredService<IPdndMetadataAccessor>();
        }

        using (var scope = provider.CreateScope())
        {
            accessorFromSecondScope = scope.ServiceProvider.GetRequiredService<IPdndMetadataAccessor>();
        }

        accessorFromFirstScope.Should().BeSameAs(sameInstanceFromFirstScope);
        accessorFromFirstScope.Should().NotBeSameAs(accessorFromSecondScope);
    }

    [Fact]
    public void AddPdndMetadata_ShouldApplyConfigureDelegate()
    {
        var services = new ServiceCollection();

        services.AddPdndMetadata(options =>
        {
            options.CaptureAllHeaders = false;
            options.MaxValueLength = 256;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PdndMetadataOptions>>().Value;

        options.CaptureAllHeaders.Should().BeFalse();
        options.MaxValueLength.Should().Be(256);
    }

    [Fact]
    public void AddPdndMetadata_ShouldUseDefaults_WhenNoConfigureDelegateProvided()
    {
        var services = new ServiceCollection();

        services.AddPdndMetadata();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PdndMetadataOptions>>().Value;

        options.CaptureAllHeaders.Should().BeTrue();
        options.ParsePdndVoucherFromAuthorizationBearer.Should().BeTrue();
    }
}
