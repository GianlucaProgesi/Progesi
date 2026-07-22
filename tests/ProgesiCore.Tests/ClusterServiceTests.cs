using System.Linq;
using System.Threading.Tasks;
using ProgesiCore.Services;
using ProgesiRepositories.InMemory;
using Xunit;

namespace Progesi.Core.Tests.Services
{
  public class ClusterServiceTests
  {
    [Fact]
    public async Task CreateOrGetCluster_Creates_New_Cluster_When_Repository_Is_Empty()
    {
      // arrange
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);

      // act
      var cluster = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2, 3 }, "desc");

      // assert
      Assert.NotNull(cluster);
      Assert.Equal("C1", cluster.Name);
      Assert.True(cluster.Id > 0);
      Assert.True(cluster.ProgesiVariableIds.SequenceEqual(new[] { 1, 2, 3 }));

      var all = await service.GetAllAsync();
      Assert.Single(all);
    }

    [Fact]
    public async Task CreateOrGetCluster_Reuses_Existing_For_Equivalent_Cluster()
    {
      // arrange
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);

      // primo cluster
      var first = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2, 3 }, "desc");

      // stesso cluster logico: stessi Id ma in ordine diverso
      var second = await service.CreateOrGetClusterAsync("C1", new[] { 3, 1, 2 }, "desc");

      // assert
      Assert.Equal(first.Id, second.Id);

      var all = await service.GetAllAsync();
      Assert.Single(all); // dedup riuscito: uno solo nello store
    }

    [Fact]
    public async Task CreateOrGetCluster_Creates_New_When_VariableIds_Differ()
    {
      // arrange
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);

      var a = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2 }, "desc");
      var b = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2, 3 }, "desc");

      // assert
      Assert.NotEqual(a.Id, b.Id);

      var all = await service.GetAllAsync();
      Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task CreateOrGetCluster_Creates_New_When_Description_Differs()
    {
      // arrange
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);

      var a = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2, 3 }, "desc-1");
      var b = await service.CreateOrGetClusterAsync("C1", new[] { 1, 2, 3 }, "desc-2");

      // assert
      Assert.NotEqual(a.Id, b.Id);

      var all = await service.GetAllAsync();
      Assert.Equal(2, all.Count);
    }
  }
}
