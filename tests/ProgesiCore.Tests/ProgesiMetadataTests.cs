using System;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataTests
    {
        [Fact]
        public void Create_And_Touch()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1); // id > 0 obbligatorio
            m.Id.Should().Be(1);
            var before = m.LastModified;
            System.Threading.Thread.Sleep(5);
            m.Touch();
            m.LastModified.Should().BeAfter(before);
        }

        [Fact]
        public void References_NoDuplicates_And_Remove()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1); // id > 0
            var u = new Uri("http://example.com/a");

            m.AddReference(u);
            m.AddReference(u);
            m.References.Count.Should().Be(1);

            m.RemoveReference(u).Should().BeTrue();
            m.References.Count.Should().Be(0);
        }

        [Fact]
        public void Snips_Add_Remove_And_Validate()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1); // id > 0
            var snip = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("http://src"));
            m.Snips.Should().HaveCount(1);

            m.RemoveSnip(Guid.NewGuid()).Should().BeFalse();
            m.RemoveSnip(snip.Id).Should().BeTrue();

            Assert.ThrowsAny<Exception>(() => m.AddSnip(Array.Empty<byte>(), "image/png"));
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 9 }, ""));
        }
    }
}
