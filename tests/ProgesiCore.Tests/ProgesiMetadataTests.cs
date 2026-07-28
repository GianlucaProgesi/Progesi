using System;
using FluentAssertions;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiMetadataTests
  {
    [Fact]
    public void Create_Is_Immutable_And_WithAdditionalInfo_Refreshes_LastModified()
    {
      var fixedTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
      var m = ProgesiMetadata.Create("user-x", id: 1, lastModifiedUtc: fixedTime);
      m.Id.Should().Be(1);
      m.LastModified.Should().Be(fixedTime);

      var updated = m.WithAdditionalInfo("notes");
      updated.AdditionalInfo.Should().Be("notes");
      updated.LastModified.Should().BeAfter(fixedTime);
      m.AdditionalInfo.Should().BeEmpty();
      m.LastModified.Should().Be(fixedTime);
    }

    [Fact]
    public void References_NoDuplicates_And_WithReferences_Replaces()
    {
      var u = new Uri("http://example.com/a");
      var m = ProgesiMetadata.Create("user-x", references: new[] { u, u }, id: 1);
      m.References.Count.Should().Be(1);

      var cleared = m.WithReferences(Array.Empty<Uri>());
      cleared.References.Count.Should().Be(0);
      m.References.Count.Should().Be(1);
    }

    [Fact]
    public void Snips_Create_WithSnips_And_Validate()
    {
      var snip = ProgesiSnip.Create(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("http://src"));
      var m = ProgesiMetadata.Create("user-x", snips: new[] { snip }, id: 1);
      m.Snips.Should().HaveCount(1);

      var cleared = m.WithSnips(Array.Empty<ProgesiSnip>());
      cleared.Snips.Should().BeEmpty();

      Assert.ThrowsAny<Exception>(() => ProgesiSnip.Create(Array.Empty<byte>(), "image/png"));
      Assert.ThrowsAny<Exception>(() => ProgesiSnip.Create(new byte[] { 9 }, ""));
    }
  }
}
