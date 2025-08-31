using System;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
    public class ProgesiHashMoreTests
    {
        [Fact]
        public void Metadata_References_OrderInsensitive_And_ContentSensitive()
        {
            // Stesso set di riferimenti, ordine diverso → hash uguale
            var a = ProgesiMetadata.Create("user", id: 1);
            a.AddReference(new Uri("http://example.com/a"));
            a.AddReference(new Uri("http://example.com/b"));

            var b = ProgesiMetadata.Create("user", id: 1);
            b.AddReference(new Uri("http://example.com/b"));
            b.AddReference(new Uri("http://example.com/a"));

            ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

            // Set diverso (sostituisco /b con /c) → hash diverso
            var c = ProgesiMetadata.Create("user", id: 1);
            c.AddReference(new Uri("http://example.com/a"));
            c.AddReference(new Uri("http://example.com/c"));

            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
        }

        [Fact]
        public void Metadata_Snips_OrderInsensitive_And_ContentSensitive()
        {
            var bytes1 = new byte[] { 1, 2, 3, 4 };
            var bytes2 = new byte[] { 5, 6, 7, 8 };
            var bytes3 = new byte[] { 9, 9, 9, 9 };

            var a = ProgesiMetadata.Create("user", id: 1);
            a.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
            a.AddSnip(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));

            var b = ProgesiMetadata.Create("user", id: 1);
            b.AddSnip(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));
            b.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));

            // Ordine diverso → hash uguale
            ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

            // Contenuto diverso → hash diverso
            var c = ProgesiMetadata.Create("user", id: 1);
            c.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
            c.AddSnip(bytes3, "application/octet-stream", "z", new Uri("http://src/3"));

            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
        }
    }
}
