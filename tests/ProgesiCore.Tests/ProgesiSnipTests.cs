using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipTests
    {
        [Fact]
        public void AddSnip_NullBytes_Throws()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            Assert.ThrowsAny<Exception>(() => m.AddSnip(null!, "image/png"));
        }

        [Fact]
        public void AddSnip_NullMime_Throws()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            Assert.ThrowsAny<Exception>(() => m.AddSnip(new byte[] { 1, 2, 3 }, null!));
        }

        [Fact]
        public void AddSnip_LongCaption_NullSource_Succeeds_AndIsRetrievable()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var caption = new string('x', 2_048);

            var snip = m.AddSnip(new byte[] { 1, 2, 3, 4 }, "image/jpeg", caption, null);

            m.Snips.Should().ContainSingle();
            snip.Should().NotBeNull();
            snip.Id.Should().NotBe(Guid.Empty);

            // l'oggetto restituito è quello contenuto nella collezione
            m.Snips.First().Id.Should().Be(snip.Id);
        }

        [Fact]
        public void AddSnip_TwoDifferentSnips_HaveDifferentIds()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            var a = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "a", new Uri("http://a"));
            var b = m.AddSnip(new byte[] { 9, 8, 7, 6 }, "image/png", "b", new Uri("https://b"));

            a.Id.Should().NotBe(Guid.Empty);
            b.Id.Should().NotBe(Guid.Empty);
            a.Id.Should().NotBe(b.Id);

            m.Snips.Should().HaveCount(2);
        }

        [Fact]
        public void RemoveSnip_WhenPresent_DecrementsCount_ThenSecondTimeFalse()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var snip = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png");

            m.Snips.Should().HaveCount(1);
            m.RemoveSnip(snip.Id).Should().BeTrue();
            m.Snips.Should().HaveCount(0);

            // rimozione ripetuta: non presente => false
            m.RemoveSnip(snip.Id).Should().BeFalse();
        }

        [Fact]
        public void RemoveSnip_NotFound_ReturnsFalse()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            m.RemoveSnip(Guid.NewGuid()).Should().BeFalse();
        }

        [Fact]
        public void AddSnip_SupportsDifferentSources()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            _ = m.AddSnip(new byte[] { 1 }, "image/png", "http", new Uri("http://a"));
            _ = m.AddSnip(new byte[] { 2 }, "image/png", "https", new Uri("https://b"));

            m.Snips.Should().HaveCount(2);
        }
    }
}
