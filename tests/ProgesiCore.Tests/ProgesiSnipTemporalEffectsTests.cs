using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiSnipTemporalEffectsTests
    {
        [Fact]
        public void AddSnip_TouchesLastModified()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var t1 = m.LastModified;

            // primo aggiornamento: potrebbe cadere nello stesso tick
            m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", null);
            var t2 = m.LastModified;
            t2.Should().BeOnOrAfter(t1);

            // secondo aggiornamento: garantiamo un tick successivo
            Thread.Sleep(5);
            m.AddSnip(new byte[] { 9, 9, 9 }, "image/jpeg", "cap2", new Uri("https://example.com"));
            m.LastModified.Should().BeAfter(t2);
        }

        [Fact]
        public void RemoveSnip_TouchesOnlyOnActualRemoval()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var s = m.AddSnip(new byte[] { 1 }, "image/png", "one", null);

            // rimozione reale -> tocca
            var t1 = m.LastModified;
            Thread.Sleep(5);
            m.RemoveSnip(s.Id).Should().BeTrue();
            var t2 = m.LastModified;
            t2.Should().BeAfter(t1);

            // rimozione ripetuta dello stesso id -> false, no touch
            Thread.Sleep(2);
            m.RemoveSnip(s.Id).Should().BeFalse();
            m.LastModified.Should().Be(t2);

            // rimozione id inesistente -> false, no touch
            m.RemoveSnip(Guid.NewGuid()).Should().BeFalse();
            m.LastModified.Should().Be(t2);
        }
    }
}
