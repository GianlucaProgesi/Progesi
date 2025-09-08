using System;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipCoverageBumpTests
    {
        [Fact]
        public void GetHashCode_CoversEqualityComponents_WithNullSource()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var snip = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", null);

            // ValueObject.GetHashCode() percorre GetEqualityComponents (Id, MimeType, Caption, Source ?? "")
            var h = snip.GetHashCode();
            h.Should().NotBe(0);
        }

        [Fact]
        public void GetHashCode_CoversEqualityComponents_WithHttpSource()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var snip = m.AddSnip(new byte[] { 9, 8, 7 }, "image/jpeg", "cap2", new Uri("http://example"));

            var h = snip.GetHashCode();
            h.Should().NotBe(0);
            snip.Source.Should().Be("http://example/"); // ToString() normalizza con trailing slash
        }

        [Fact]
        public void RemoveSnip_WhenEmpty_ReturnsFalse_AndDoesNotTouch()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var before = m.LastModified;

            Thread.Sleep(5);
            var r = m.RemoveSnip(Guid.NewGuid());

            r.Should().BeFalse();
            m.LastModified.Should().Be(before); // no touch sul ramo "not found"
        }
    }
}
