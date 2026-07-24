using System.Linq;
using System.Threading.Tasks;
using ProgesiCore;
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

    // ---- R2-G: referential integrity ----

    [Fact]
    public async Task CreateOrGetCluster_StrictReject_When_VariableId_Missing()
    {
      // arrange: var 2 exists, var 9 does NOT
      var clusterRepo = new InMemoryVariableClusterRepository();
      var varRepo = new InMemoryVariableRepository();
      await varRepo.SaveAsync(new ProgesiVariable(2, "v2", 1.0));
      var service = new ClusterService(clusterRepo, varRepo);

      // act + assert: creating a cluster referencing missing var 9 is rejected
      await Assert.ThrowsAsync<System.ArgumentException>(
        () => service.CreateOrGetClusterAsync("C", new[] { 2, 9 }, "d"));

      var all = await service.GetAllAsync();
      Assert.Empty(all);
    }

    [Fact]
    public async Task CreateOrGetCluster_Succeeds_When_All_Variables_Exist()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var varRepo = new InMemoryVariableRepository();
      await varRepo.SaveAsync(new ProgesiVariable(2, "v2", 1.0));
      await varRepo.SaveAsync(new ProgesiVariable(9, "v9", 2.0));
      var service = new ClusterService(clusterRepo, varRepo);

      var cluster = await service.CreateOrGetClusterAsync("C", new[] { 2, 9 }, "d");

      Assert.NotNull(cluster);
      Assert.True(cluster.ProgesiVariableIds.SequenceEqual(new[] { 2, 9 }));
    }

    [Fact]
    public async Task CascadeRemove_Removes_Variable_And_Keeps_NonEmpty_Cluster()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);
      var c = await service.CreateOrGetClusterAsync("C", new[] { 2, 9 }, "d");

      var affected = await service.CascadeRemoveVariableFromClustersAsync(9);

      Assert.Equal(1, affected);
      var reloaded = await service.GetByIdAsync(c.Id);
      Assert.NotNull(reloaded);
      Assert.True(reloaded!.ProgesiVariableIds.SequenceEqual(new[] { 2 }));
    }

    [Fact]
    public async Task CascadeRemove_Deletes_Cluster_When_It_Becomes_Empty()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);
      var c = await service.CreateOrGetClusterAsync("C", new[] { 9 }, "d");

      var affected = await service.CascadeRemoveVariableFromClustersAsync(9);

      Assert.Equal(1, affected);
      var reloaded = await service.GetByIdAsync(c.Id);
      Assert.Null(reloaded);
      var all = await service.GetAllAsync();
      Assert.Empty(all);
    }
  }
}
