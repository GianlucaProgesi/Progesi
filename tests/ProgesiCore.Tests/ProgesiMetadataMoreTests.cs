using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataMoreTests
    {
        [Fact]
        public void Create_Touch_Updates_LastModified()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1);
            var before = m.LastModified;
            System.Threading.Thread.Sleep(5);
            m.Touch();
            m.LastModified.Should().BeOnOrAfter(before);
            m.LastModified.Should().BeAfter(before);
        }

        [Fact]
        public void Snips_Add_Then_Remove_Works_Idempotently()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1);

            var snip = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("http://src"));
            m.Snips.Should().HaveCount(1);

            // rimozione presente → true
            m.RemoveSnip(snip.Id).Should().BeTrue();
            m.Snips.Should().BeEmpty();

            // rimozione assente → false
            m.RemoveSnip(snip.Id).Should().BeFalse();
        }

        [Fact]
        public void Snips_Add_Validates_Input()
        {
            var m = ProgesiMetadata.Create("user-x", id: 1);

            Assert.ThrowsAny<Exception>(() => m.AddSnip(Array.Empty<byte>(), "image/png"));
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 9 }, ""));
        }

        [Fact]
        public void References_Order_Does_Not_Affect_Hash()
        {
            var a = ProgesiMetadata.Create("user", id: 1);
            a.AddReference(new Uri("http://example.com/a"));
            a.AddReference(new Uri("http://example.com/b"));

            var b = ProgesiMetadata.Create("user", id: 1);
            b.AddReference(new Uri("http://example.com/b"));
            b.AddReference(new Uri("http://example.com/a"));

            ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));
        }

        [Fact]
        public void Snips_Content_Affects_Hash()
        {
            byte[] bytes1 = new byte[] { 1, 2, 3, 4 };
            byte[] bytes2 = new byte[] { 9, 9, 9, 9 };

            var a = ProgesiMetadata.Create("user", id: 1);
            _ = a.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));

            var b = ProgesiMetadata.Create("user", id: 1);
            _ = b.AddSnip(bytes2, "application/octet-stream", "x", new Uri("http://src/1"));

            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(b));
        }
    }
}
