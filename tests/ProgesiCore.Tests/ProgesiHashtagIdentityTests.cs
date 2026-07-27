using System;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.InMemory;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiHashtagIdentityTests
  {
    [Fact]
    public void Variable_Hashtag_Equals_ProgesiHash_Compute()
    {
      var v = new ProgesiVariable(1, "Load", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 4 });
      v.Hashtag.Should().Be(ProgesiHash.Compute(v));
    }

    [Fact]
    public void Variable_Hashtag_Matches_R2I_Golden_Hashes()
    {
      var noMeta = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 });
      var singleMeta = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 4 });

      noMeta.Hashtag.Should().Be("1ebd9965c6a742c6077d8ad6767a77f51eb1cf21d6f91ac2915904a758fc6aa7");
      singleMeta.Hashtag.Should().Be("f0f23acbfe86bd3ae84018d5a4e540045d87b7b6be5b657fdab575e658109fa1");
    }

    [Fact]
    public void Metadata_Hashtag_Equals_ProgesiHash_Compute()
    {
      var m = ProgesiMetadata.Create("author", "notes", id: 3);
      m.Hashtag.Should().Be(ProgesiHash.Compute(m));
    }

    [Fact]
    public void Snip_Hashtag_Equals_ProgesiHash_Compute()
    {
      var snip = ProgesiSnip.Create(new byte[] { 1, 2, 3 }, "image/png", "cap", new Uri("https://example.com/x"));
      snip.Hashtag.Should().Be(ProgesiHash.Compute(snip));
    }

    [Fact]
    public void Cluster_Hashtag_Equals_ProgesiHash_Compute_Not_Legacy_Format()
    {
      var cluster = ProgesiVariableCluster.Rehydrate(3, "HashC", new[] { 9 }, null);

      cluster.Hashtag.Should().Be(ProgesiHash.Compute(cluster));
      cluster.Hashtag.Should().NotBe("3|HashC|9");
    }

    [Fact]
    public async System.Threading.Tasks.Task Cluster_GetByHashtagAsync_Resolves_Legacy_Format()
    {
      var repo = new InMemoryVariableClusterRepository();
      var cluster = ProgesiVariableCluster.Rehydrate(3, "HashC", new[] { 9 }, null);
      await repo.SaveAsync(cluster);

      var legacy = "3|HashC|9";
      var loaded = await repo.GetByHashtagAsync(legacy);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(3);
    }

    [Fact]
    public void HashtagSchemeVersion_Is_One()
    {
      ProgesiHash.HashtagSchemeVersion.Should().Be(1);
    }
  }
}
