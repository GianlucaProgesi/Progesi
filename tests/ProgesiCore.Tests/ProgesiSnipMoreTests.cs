using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipMoreTests
    {
        [Fact]
        public void AddSnip_Returns_Item_And_Collection_Contains_It()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1);

            var snip = m.AddSnip(new byte[] { 10, 11, 12 }, "image/png", "cap", new Uri("http://src"));
            snip.Should().NotBeNull();

            m.Snips.Should().HaveCount(1);
            m.Snips.Any(s => s.Id == snip.Id).Should().BeTrue();
        }

        [Fact]
        public void RemoveSnip_By_Id_Removes_Only_That_Item()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1);

            var s1 = m.AddSnip(new byte[] { 1 }, "application/octet-stream", "a", new Uri("http://src/a"));
            var s2 = m.AddSnip(new byte[] { 2 }, "application/octet-stream", "b", new Uri("http://src/b"));

            m.Snips.Should().HaveCount(2);

            m.RemoveSnip(s1.Id).Should().BeTrue();
            m.Snips.Should().HaveCount(1);
            m.Snips.Single().Id.Should().Be(s2.Id);

            m.RemoveSnip(Guid.NewGuid()).Should().BeFalse();
        }
    }
}
