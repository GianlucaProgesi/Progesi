using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.InMemory;
using Xunit;

namespace ProgesiCore.Tests
{
  public class InMemoryVariableClusterRepositoryTests
  {
    [Fact]
    public async Task SaveAsync_Then_GetByIdAsync_RoundTrips_Cluster()
    {
      var repo = new InMemoryVariableClusterRepository();
      var cluster = ProgesiVariableCluster.Rehydrate(7, "RepoC", new[] { 1, 2 }, "desc");

      await repo.SaveAsync(cluster);
      var loaded = await repo.GetByIdAsync(7);

      loaded.Should().NotBeNull();
      loaded!.Name.Should().Be("RepoC");
      loaded.ProgesiVariableIds.Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetByHashtagAsync_Finds_Saved_Cluster()
    {
      var repo = new InMemoryVariableClusterRepository();
      var cluster = ProgesiVariableCluster.Rehydrate(3, "HashC", new[] { 9 }, null);
      await repo.SaveAsync(cluster);

      var loaded = await repo.GetByHashtagAsync(cluster.Hashtag);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(3);
    }

    [Fact]
    public async Task GetAllAsync_Returns_All_Saved_Clusters()
    {
      var repo = new InMemoryVariableClusterRepository();
      await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "A", new[] { 1 }, null));
      await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(2, "B", new[] { 2 }, null));

      var all = await repo.GetAllAsync();

      all.Should().HaveCount(2);
      all.Select(c => c.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task DeleteAsync_Removes_Cluster()
    {
      var repo = new InMemoryVariableClusterRepository();
      await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(4, "Del", new[] { 1 }, null));

      (await repo.DeleteAsync(4)).Should().BeTrue();
      (await repo.GetByIdAsync(4)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteManyAsync_Removes_Only_Existing_Ids()
    {
      var repo = new InMemoryVariableClusterRepository();
      await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "A", new[] { 1 }, null));
      await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(2, "B", new[] { 2 }, null));

      var removed = await repo.DeleteManyAsync(new[] { 1, 99, 2 });

      removed.Should().Be(2);
      (await repo.GetAllAsync()).Should().BeEmpty();
    }
  }
}
