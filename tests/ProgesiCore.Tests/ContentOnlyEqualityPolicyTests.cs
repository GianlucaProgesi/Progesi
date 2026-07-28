using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ContentOnlyEqualityPolicyTests
  {
    [Fact]
    public void Variable_Same_Content_Different_Id_Is_Equal_And_Hash_Equal()
    {
      var a = new ProgesiVariable(1, "Load", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 4 });
      var b = new ProgesiVariable(2, "Load", 42, new[] { 1, 2, 3 }, metadataIds: new[] { 4 });

      a.Should().Be(b);
      a.GetHashCode().Should().Be(b.GetHashCode());
      a.Hashtag.Should().Be(b.Hashtag);
    }

    [Fact]
    public void Metadata_Same_Content_Different_Id_Is_Equal_And_Hash_Equal()
    {
      var refs = new[] { new Uri("https://example.com/a") };
      var a = ProgesiMetadata.Create("author", "notes", refs, id: 1, lastModifiedUtc: DateTime.UtcNow);
      var b = ProgesiMetadata.Create("author", "notes", refs, id: 2, lastModifiedUtc: a.LastModified);

      a.Should().Be(b);
      a.GetHashCode().Should().Be(b.GetHashCode());
      a.Hashtag.Should().Be(b.Hashtag);
    }

    [Fact]
    public void Snip_Same_Content_Different_Guid_Is_Equal_And_Hash_Equal()
    {
      var bytes = new byte[] { 1, 2, 3 };
      var a = ProgesiSnip.Create(bytes, "image/png", "cap", new Uri("https://example.com/x"));
      var b = ProgesiSnip.Create(bytes, "image/png", "cap", new Uri("https://example.com/x"));

      a.Id.Should().NotBe(b.Id);
      a.Should().Be(b);
      a.GetHashCode().Should().Be(b.GetHashCode());
      a.Hashtag.Should().Be(b.Hashtag);
    }

    [Fact]
    public void ProgesiMetadata_Has_No_Public_InPlace_Mutators()
    {
      var mutatorNames = new[]
      {
        "UpdateAdditionalInfo",
        "AddReference",
        "AddReferences",
        "RemoveReference",
        "AddSnip",
        "RemoveSnip",
        "Touch"
      };

      var methods = typeof(ProgesiMetadata)
          .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
          .Select(m => m.Name)
          .ToArray();

      methods.Should().NotContain(mutatorNames);
      methods.Should().Contain(new[] { "WithAdditionalInfo", "WithReferences", "WithSnips" });
    }
  }
}
