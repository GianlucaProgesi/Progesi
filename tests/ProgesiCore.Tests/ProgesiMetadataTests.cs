using System;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataTests
    {
        [Fact]
        public void Create_Update_AddRemove_Reference_Snip()
        {
            // Id obbligatorio > 0
            var m = ProgesiMetadata.Create("gianluca", "first", id: 1);
            m.CreatedBy.Should().Be("gianluca");
            m.AdditionalInfo.Should().Be("first");

            m.UpdateAdditionalInfo("updated");
            m.AdditionalInfo.Should().Be("updated");

            var u1 = new Uri("https://example.com/a/");
            var u2 = new Uri("https://example.com/b");
            m.AddReference(u1);
            m.AddReference(u2);
            m.References.Should().HaveCount(2);

            var removed = m.RemoveReference(u1);
            removed.Should().BeTrue();
            m.References.Should().HaveCount(1);

            var snip = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap");
            m.Snips.Should().ContainSingle();
            m.RemoveSnip(snip.Id).Should().BeTrue();
            m.Snips.Should().BeEmpty();
        }
    }
}
