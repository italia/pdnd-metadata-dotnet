// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Models;

public class PdndCallerMetadataTests
{
    [Fact]
    public void Add_ShouldStoreItems_CaseInsensitiveKeys()
    {
        var md = new PdndCallerMetadata();

        md.Add("k", "v1", PdndMetadataSource.Header);
        md.Add("K", "v2", PdndMetadataSource.Header);

        md.GetValues("k").Should().BeEquivalentTo(new[] { "v1", "v2" });
        md.GetValues("K").Should().BeEquivalentTo(new[] { "v1", "v2" });
    }

    [Fact]
    public void GetFirstValue_ShouldReturnNull_WhenMissing()
    {
        var md = new PdndCallerMetadata();
        md.GetFirstValue("missing").Should().BeNull();
    }

    [Fact]
    public void GetFirstValue_ShouldReturnFirstInsertedValue()
    {
        var md = new PdndCallerMetadata();
        md.Add("k", "v1", PdndMetadataSource.Header);
        md.Add("k", "v2", PdndMetadataSource.Header);

        md.GetFirstValue("k").Should().Be("v1");
    }

    [Fact]
    public void Clone_ShouldCreateShallowCopy_WithSameItems()
    {
        var md = new PdndCallerMetadata();
        md.Add("k", "v1", PdndMetadataSource.Header);
        md.Add("k", "v2", PdndMetadataSource.Claims);

        var clone = md.Clone();

        clone.Should().NotBeSameAs(md);
        clone.GetValues("k").Should().BeEquivalentTo(new[] { "v1", "v2" });
        clone.Items.Keys.Should().Contain("k");
    }

    [Fact]
    public void Add_ShouldIgnoreEmptyKeyOrValue()
    {
        var md = new PdndCallerMetadata();

        md.Add("", "v", PdndMetadataSource.Header);
        md.Add("k", "", PdndMetadataSource.Header);

        md.Items.Should().BeEmpty();
    }
}