// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Tests.Models;

/// <summary>
/// Keeps the deprecated <c>Items</c> property covered until it is removed in 3.0.0.
/// </summary>
/// <remarks>
/// The suppression is scoped to this file rather than set project-wide. The build treats warnings
/// as errors, so any other use of the deprecated member fails the build, which is the intent: the
/// shim exists for consumers who have not migrated, not for this repository.
/// </remarks>
public sealed class PdndCallerMetadataObsoleteItemsTests
{
    [Fact]
    public void Items_ReturnsTheSameContentAsSnapshot()
    {
        var metadata = new PdndCallerMetadata();
        metadata.Add("a", "1", PdndMetadataSource.Header);
        metadata.Add("b", "2", PdndMetadataSource.Claims);

#pragma warning disable CS0618 // deliberately exercising the deprecated member
        var items = metadata.Items;
#pragma warning restore CS0618

        var snapshot = metadata.Snapshot();

        items.Keys.Should().BeEquivalentTo(snapshot.Keys);
        items["a"][0].Value.Should().Be("1");
        items["b"][0].Value.Should().Be("2");
    }

    [Fact]
    public void Items_ReturnsAnIndependentCopy()
    {
#pragma warning disable CS0618 // deliberately exercising the deprecated member
        var metadata = new PdndCallerMetadata();
        var before = metadata.Items;

        metadata.Add("a", "1", PdndMetadataSource.Header);

        var after = metadata.Items;
#pragma warning restore CS0618

        before.Should().BeEmpty();
        after.Should().HaveCount(1);
    }
}
