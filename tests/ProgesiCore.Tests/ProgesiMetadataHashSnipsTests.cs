using System;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataHashSnipsTests
    {
        [Fact]
        public void Hash_Changes_When_Snip_Is_Added()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var h1 = ProgesiHash.Compute(m); // senza snips: il foreach degli snips non itera

            _ = m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("https://s"));
            var h2 = ProgesiHash.Compute(m); // con snip: il foreach degli snips itera (copre i yield relativi)

            h2.Should().NotBe(h1);
        }

        [Fact]
        public void Hash_Differs_Between_NullSource_And_NonNullSource_Snips()
        {
            var m1 = ProgesiMetadata.Create("user", id: 1);
            _ = m1.AddSnip(new byte[] { 1 }, "image/png", "a", null);

            var m2 = ProgesiMetadata.Create("user", id: 2);
            _ = m2.AddSnip(new byte[] { 1 }, "image/png", "a", new Uri("http://x"));

            var h1 = ProgesiHash.Compute(m1);
            var h2 = ProgesiHash.Compute(m2);

            h1.Should().NotBe(h2); // differisce per il campo Source nel ValueObject dello snip
        }
    }
}
