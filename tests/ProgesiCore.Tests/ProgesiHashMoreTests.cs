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
      var a = ProgesiMetadata.Create("user", references: new[]
      {
        new Uri("http://example.com/a"),
        new Uri("http://example.com/b")
      }, id: 1);

      var b = ProgesiMetadata.Create("user", references: new[]
      {
        new Uri("http://example.com/b"),
        new Uri("http://example.com/a")
      }, id: 1);

      ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

      var c = ProgesiMetadata.Create("user", references: new[]
      {
        new Uri("http://example.com/a"),
        new Uri("http://example.com/c")
      }, id: 1);

      ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
    }

    [Fact]
    public void Metadata_Snips_OrderInsensitive_And_ContentSensitive()
    {
      byte[] bytes1 = new byte[] { 1, 2, 3, 4 };
      byte[] bytes2 = new byte[] { 5, 6, 7, 8 };
      byte[] bytes3 = new byte[] { 9, 9, 9, 9 };

      var snip1a = ProgesiSnip.Create(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
      var snip2a = ProgesiSnip.Create(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));
      var a = ProgesiMetadata.Create("user", snips: new[] { snip1a, snip2a }, id: 1);

      var snip2b = ProgesiSnip.Create(bytes2, "application/octet-stream", "y", new Uri("http://src/2"));
      var snip1b = ProgesiSnip.Create(bytes1, "application/octet-stream", "x", new Uri("http://src/1"));
      var b = ProgesiMetadata.Create("user", snips: new[] { snip2b, snip1b }, id: 1);

      ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));

      var snip3c = ProgesiSnip.Create(bytes3, "application/octet-stream", "z", new Uri("http://src/3"));
      var c = ProgesiMetadata.Create("user", snips: new[] { snip1a, snip3c }, id: 1);

      ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(c));
    }
  }
}
