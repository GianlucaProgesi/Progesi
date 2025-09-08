using System;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataEdgeTests
    {
        [Fact]
        public void AdditionalInfo_LongString_Updates_AndTouches()
        {
            var m = ProgesiMetadata.Create("user", id: 1, additionalInfo: "");
            var before = m.LastModified;

            var longText = new string('α', 10_000); // stringa lunga
            Thread.Sleep(5); // garantisce delta temporale
            m.UpdateAdditionalInfo(longText);

            m.AdditionalInfo.Should().Be(longText);
            m.LastModified.Should().BeAfter(before);
        }

        [Fact]
        public void Touch_Multiple_Timestamps_Monotonic()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var t1 = m.LastModified;

            Thread.Sleep(2);
            m.Touch();
            var t2 = m.LastModified;

            Thread.Sleep(2);
            m.Touch();
            var t3 = m.LastModified;

            t2.Should().BeAfter(t1);
            t3.Should().BeAfter(t2);
        }

        [Fact]
        public void RemoveReference_Twice_FirstTrue_SecondFalse()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var u = new Uri("http://a");

            m.AddReference(u);
            m.References.Count.Should().Be(1);

            m.RemoveReference(u).Should().BeTrue();
            m.References.Count.Should().Be(0);

            // già rimossa
            m.RemoveReference(u).Should().BeFalse();
        }

        [Fact]
        public void Snip_Add_Remove_ReAdd_DoesNotThrow_AndCountsOk()
        {
            var m = ProgesiMetadata.Create("user", id: 1);

            var s1 = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("http://src"));
            m.Snips.Should().HaveCount(1);

            m.RemoveSnip(s1.Id).Should().BeTrue();
            m.Snips.Should().HaveCount(0);

            // re-add nuovo snip (nuovo Id)
            var s2 = m.AddSnip(new byte[] { 9, 9, 9 }, "image/png", "cap2", null);
            m.Snips.Should().HaveCount(1);
            s2.Id.Should().NotBe(Guid.Empty);
        }
    }
}
