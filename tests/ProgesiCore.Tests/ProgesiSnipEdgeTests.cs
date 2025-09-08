using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipEdgeTests
    {
        [Fact]
        public void AddSnip_WhitespaceMime_Throws()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 1, 2, 3 }, "   "));
        }

        [Fact]
        public void AddSnip_EmptyCaption_And_NullSource_Succeeds()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var snip = m.AddSnip(new byte[] { 9, 8, 7 }, "image/png", "", null);
            snip.Should().NotBeNull();
            m.Snips.Should().ContainSingle();
        }

        [Fact]
        public void AddSnip_LargePayload_Succeeds()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var big = Enumerable.Range(0, 16_384).Select(i => (byte)(i % 256)).ToArray();
            var snip = m.AddSnip(big, "image/jpeg", "large", new Uri("https://example.com"));
            snip.Id.Should().NotBe(Guid.Empty);
            m.Snips.Should().HaveCount(1);
        }

        [Fact]
        public void AddTwoSnips_DistinctIds_And_RemoveAll()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var a = m.AddSnip(new byte[] { 1 }, "image/png", "a", new Uri("http://a"));
            var b = m.AddSnip(new byte[] { 2 }, "image/png", "b", new Uri("https://b"));

            a.Id.Should().NotBe(b.Id);
            m.Snips.Should().HaveCount(2);

            m.RemoveSnip(a.Id).Should().BeTrue();
            m.RemoveSnip(b.Id).Should().BeTrue();
            m.Snips.Should().BeEmpty();
        }

        [Fact]
        public void RemoveSnip_Twice_ReturnsFalse_SecondTime()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var s = m.AddSnip(new byte[] { 3, 3, 3 }, "image/png");
            m.RemoveSnip(s.Id).Should().BeTrue();
            m.RemoveSnip(s.Id).Should().BeFalse();
        }
    }
}
