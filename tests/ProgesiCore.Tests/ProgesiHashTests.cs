using System;
using System.Linq;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiHashTests
    {
        [Fact]
        public void VariableHash_IsDeterministic_And_IndependentOfDependsOrder()
        {
            var v1 = new ProgesiVariable(1, "N", 42, new[] { 3, 1, 2 }, metadataId: 7);
            var v2 = new ProgesiVariable(2, "N", 42, new[] { 2, 3, 1 }, metadataId: 7);

            var h1 = ProgesiHash.Compute(v1);
            var h2 = ProgesiHash.Compute(v2);

            h1.Should().Be(h2);
        }

        [Fact]
        public void MetadataHash_Ignores_Id_And_LastModified_ButDependsOnContent()
        {
            var refsA = new[] { new Uri("https://Site.com/a/"), new Uri("https://site.com/b") };
            var m1 = ProgesiMetadata.Create("usr", "info", refsA, lastModifiedUtc: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), id: 10);
            var m2 = ProgesiMetadata.Create("usr", "info", refsA.Reverse(), lastModifiedUtc: DateTime.UtcNow, id: 999);

            var h1 = ProgesiHash.Compute(m1);
            var h2 = ProgesiHash.Compute(m2);

            h1.Should().Be(h2);

            var m3 = ProgesiMetadata.Create("usr", "DIFFERENT", refsA, id: 11);
            ProgesiHash.Compute(m3).Should().NotBe(h1);
        }

        [Fact]
        public void MetadataHash_UsesSnipContent_NotCaption()
        {
            var m1 = ProgesiMetadata.Create("usr", id: 21);
            m1.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "a");

            var m2 = ProgesiMetadata.Create("usr", id: 22);
            m2.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "different caption");

            ProgesiHash.Compute(m1).Should().Be(ProgesiHash.Compute(m2));
        }
    }
}
