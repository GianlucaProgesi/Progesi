using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataMoreEdgeTests
    {
        [Fact]
        public void AdditionalInfo_NullEmptyLong_Touches()
        {
            var m = ProgesiMetadata.Create("user", id: 1, additionalInfo: "x");
            var t1 = m.LastModified;

            m.UpdateAdditionalInfo(null);
            m.AdditionalInfo.Should().Be("");
            var t2 = m.LastModified;
            t2.Should().BeAfter(t1);

            var longText = new string('Z', 4096);
            Thread.Sleep(2);
            m.UpdateAdditionalInfo(longText);
            m.AdditionalInfo.Should().Be(longText);
            m.LastModified.Should().BeAfter(t2);
        }

        [Fact]
        public void References_AddNullAndDuplicates_ResultHasOnlyDistinctValid()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            m.AddReference(null);
            m.AddReference(new Uri("http://a"));
            m.AddReference(new Uri("http://a")); // dup
            m.AddReference(new Uri("https://b"));

            m.References.Select(u => u.ToString())
                .Should().BeEquivalentTo(new[] { "http://a/", "https://b/" });
        }

        [Fact]
        public void RemoveReference_Sequence_TouchOnlyOnChange()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var a = new Uri("http://a");
            var b = new Uri("https://b");

            m.AddReference(a);
            var t1 = m.LastModified;

            Thread.Sleep(2);
            var removed = m.RemoveReference(a);
            removed.Should().BeTrue();
            var t2 = m.LastModified;
            t2.Should().BeAfter(t1);

            // rimuovere di nuovo non cambia nulla
            Thread.Sleep(2);
            m.RemoveReference(a).Should().BeFalse();
            m.LastModified.Should().Be(t2);

            // rimuovere URI mai aggiunto -> false, no touch
            m.RemoveReference(b).Should().BeFalse();
            m.LastModified.Should().Be(t2);
        }

        [Fact]
        public void Touch_IsMonotonic()
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
    }
}
