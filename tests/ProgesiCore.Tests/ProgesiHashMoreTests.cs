using System;
using FluentAssertions;
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

            _ = ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

            // Set diverso (sostituisco /b con /c) → hash diverso
            var c = ProgesiMetadata.Create("user", id: 1);
            c.AddReference(new Uri("http://example.com/a"));
            c.AddReference(new Uri("http://example.com/c"));

            _ = ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
        }

        [Fact]
        public void Metadata_Snips_OrderInsensitive_And_ContentSensitive()
        {
            byte[] bytes1 = new byte[] { 1, 2, 3, 4 };
            byte[] bytes2 = new byte[] { 5, 6, 7, 8 };
            byte[] bytes3 = new byte[] { 9, 9, 9, 9 };

            var a = ProgesiMetadata.Create("user", id: 1);
            _ = a.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
            _ = a.AddSnip(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));

            var b = ProgesiMetadata.Create("user", id: 1);
            _ = b.AddSnip(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));
            _ = b.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));

            // Ordine diverso → hash uguale
            _ = ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

            // Contenuto diverso → hash diverso
            var c = ProgesiMetadata.Create("user", id: 1);
            _ = c.AddSnip(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
            _ = c.AddSnip(bytes3, "application/octet-stream", "z", new Uri("http://src/3"));

            _ = ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
        }
    }
}
