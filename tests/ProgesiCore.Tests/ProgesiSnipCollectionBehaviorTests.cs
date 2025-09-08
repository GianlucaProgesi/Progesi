using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipCollectionBehaviorTests
    {
        [Fact]
        public void AddTwoSnips_KeepOrder_IdsAreUnique()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            var a = m.AddSnip(new byte[] { 1, 2 }, "image/png", "a", new Uri("http://a"));
            var b = m.AddSnip(new byte[] { 3, 4 }, "image/png", "b", new Uri("https://b"));

            var arr = m.Snips.ToArray();
            arr.Should().HaveCount(2);
            arr[0].Id.Should().Be(a.Id);
            arr[1].Id.Should().Be(b.Id);
            a.Id.Should().NotBe(Guid.Empty);
            b.Id.Should().NotBe(Guid.Empty);
            a.Id.Should().NotBe(b.Id);
        }

        [Fact]
        public void AddSnip_Validations_NullOrWhitespaceMime_And_EmptyBytes()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            // bytes null -> eccezione
            Assert.ThrowsAny<Exception>(() => m.AddSnip(null!, "image/png"));

            // bytes vuoti -> eccezione
            Assert.ThrowsAny<Exception>(() => m.AddSnip(Array.Empty<byte>(), "image/png"));

            // mime null/whitespace -> eccezione
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 1 }, null!));
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 1 }, "   "));
        }

        [Fact]
        public void AddManySnips_ThenRemoveAll_CollectionEndsEmpty()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            var ids = Enumerable.Range(0, 5)
                .Select(i => m.AddSnip(new byte[] { (byte)i }, "image/png", $"snip-{i}", null).Id)
                .ToArray();

            m.Snips.Should().HaveCount(5);

            foreach (var id in ids)
            {
                m.RemoveSnip(id).Should().BeTrue();
            }

            m.Snips.Should().BeEmpty();
        }
    }
}
