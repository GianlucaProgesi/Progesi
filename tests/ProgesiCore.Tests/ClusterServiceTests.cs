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
    public async Task UpdateCluster_StrictReject_When_VariableId_Missing()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var varRepo = new InMemoryVariableRepository();
      await varRepo.SaveAsync(new ProgesiVariable(2, "v2", 1.0));
      var service = new ClusterService(clusterRepo, varRepo);

      var created = await service.CreateOrGetClusterAsync("C", new[] { 2 }, "d");

      await Assert.ThrowsAsync<System.ArgumentException>(
        () => service.UpdateClusterAsync(created.Id, "C", new[] { 2, 9 }, "d"));

      var reloaded = await service.GetByIdAsync(created.Id);
      Assert.NotNull(reloaded);
      Assert.True(reloaded!.ProgesiVariableIds.SequenceEqual(new[] { 2 }));
    }

    [Fact]
    public async Task UpdateCluster_Succeeds_When_All_Variables_Exist()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var varRepo = new InMemoryVariableRepository();
      await varRepo.SaveAsync(new ProgesiVariable(2, "v2", 1.0));
      await varRepo.SaveAsync(new ProgesiVariable(9, "v9", 2.0));
      var service = new ClusterService(clusterRepo, varRepo);

      var created = await service.CreateOrGetClusterAsync("C", new[] { 2 }, "d");

      var updated = await service.UpdateClusterAsync(created.Id, "C2", new[] { 2, 9 }, "d2");

      Assert.Equal(created.Id, updated.Id);
      Assert.Equal("C2", updated.Name);
      Assert.True(updated.ProgesiVariableIds.SequenceEqual(new[] { 2, 9 }));
      Assert.Equal("d2", updated.Description);
    }

    [Fact]
    public async Task CascadeRemove_Removes_Variable_And_Keeps_NonEmpty_Cluster()
    {
      var clusterRepo = new InMemoryVariableClusterRepository();
      var service = new ClusterService(clusterRepo);
      var c = await service.CreateOrGetClusterAsync("C", new[] { 2, 9 }, "d");

      var affected = await service.CascadeRemoveVariableFromClustersAsync(9);

      Assert.Equal(1, affected.Applied);
      Assert.True(affected.IsFullySuccessful);
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

      Assert.Equal(1, affected.Applied);
      Assert.True(affected.IsFullySuccessful);
      var reloaded = await service.GetByIdAsync(c.Id);
      Assert.Null(reloaded);
      var all = await service.GetAllAsync();
      Assert.Empty(all);
    }

    [Fact]
    public async Task CascadeRemove_Continues_After_Partial_Failure_And_Reports_Failed_Clusters()
    {
      var clusterRepo = new ThrowingOnNthCascadeMutationRepository(failOnNthMutation: 2, removedVariableId: 9);
      var service = new ClusterService(clusterRepo);

      await service.CreateOrGetClusterAsync("C1", new[] { 9, 1 }, "d");
      var failing = await service.CreateOrGetClusterAsync("C2", new[] { 9, 2 }, "d");
      await service.CreateOrGetClusterAsync("C3", new[] { 9, 3 }, "d");

      var result = await service.CascadeRemoveVariableFromClustersAsync(9);

      Assert.Equal(2, result.Applied);
      Assert.False(result.IsFullySuccessful);
      Assert.Equal(new[] { failing.Id }, result.FailedClusterIds);

      var c1 = await service.GetByIdAsync(1);
      var c2 = await service.GetByIdAsync(failing.Id);
      var c3 = await service.GetByIdAsync(3);
      Assert.NotNull(c1);
      Assert.NotNull(c2);
      Assert.NotNull(c3);
      Assert.True(c1!.ProgesiVariableIds.SequenceEqual(new[] { 1 }));
      Assert.True(c2!.ProgesiVariableIds.SequenceEqual(new[] { 2, 9 }));
      Assert.True(c3!.ProgesiVariableIds.SequenceEqual(new[] { 3 }));
    }

    [Fact]
    public async Task SimulatedDeleteVariable_Skips_Variable_Delete_On_Partial_Cascade_Failure()
    {
      var varRepo = new InMemoryVariableRepository();
      await varRepo.SaveAsync(new ProgesiVariable(9, "v9", 2.0));

      var clusterRepo = new ThrowingOnNthCascadeMutationRepository(failOnNthMutation: 2, removedVariableId: 9);
      var clusterService = new ClusterService(clusterRepo);
      await clusterService.CreateOrGetClusterAsync("C1", new[] { 9, 1 }, "d");
      await clusterService.CreateOrGetClusterAsync("C2", new[] { 9, 2 }, "d");

      var cascade = await clusterService.CascadeRemoveVariableFromClustersAsync(9);

      var deleteVariable = cascade.IsFullySuccessful;
      if (deleteVariable)
        await varRepo.DeleteAsync(9);

      Assert.False(deleteVariable);
      Assert.NotNull(await varRepo.GetByIdAsync(9));
    }

    private sealed class ThrowingOnNthCascadeMutationRepository : IProgesiVariableClusterRepository
    {
      private readonly InMemoryVariableClusterRepository _inner = new InMemoryVariableClusterRepository();
      private readonly int _failOnNthMutation;
      private readonly int _removedVariableId;
      private int _cascadeMutationCount;

      public ThrowingOnNthCascadeMutationRepository(int failOnNthMutation, int removedVariableId)
      {
        _failOnNthMutation = failOnNthMutation;
        _removedVariableId = removedVariableId;
      }

      public async Task<ProgesiVariableCluster> SaveAsync(ProgesiVariableCluster cluster, System.Threading.CancellationToken ct = default)
      {
        var existing = await _inner.GetByIdAsync(cluster.Id, ct).ConfigureAwait(false);
        if (existing != null
            && existing.ProgesiVariableIds.Contains(_removedVariableId)
            && !cluster.ProgesiVariableIds.Contains(_removedVariableId))
        {
          _cascadeMutationCount++;
          if (_cascadeMutationCount == _failOnNthMutation)
            throw new System.InvalidOperationException($"Simulated cascade failure on cluster {cluster.Id}");
        }

        return await _inner.SaveAsync(cluster, ct).ConfigureAwait(false);
      }

      public Task<ProgesiVariableCluster?> GetByIdAsync(int id, System.Threading.CancellationToken ct = default)
        => _inner.GetByIdAsync(id, ct);

      public Task<ProgesiVariableCluster?> GetByHashtagAsync(string hashtag, System.Threading.CancellationToken ct = default)
        => _inner.GetByHashtagAsync(hashtag, ct);

      public Task<System.Collections.Generic.IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(System.Threading.CancellationToken ct = default)
        => _inner.GetAllAsync(ct);

      public async Task<bool> DeleteAsync(int id, System.Threading.CancellationToken ct = default)
      {
        var existing = await _inner.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (existing != null
            && existing.ProgesiVariableIds.Count == 1
            && existing.ProgesiVariableIds.Contains(_removedVariableId))
        {
          _cascadeMutationCount++;
          if (_cascadeMutationCount == _failOnNthMutation)
            throw new System.InvalidOperationException($"Simulated cascade failure on cluster {id}");
        }

        return await _inner.DeleteAsync(id, ct).ConfigureAwait(false);
      }

      public Task<int> DeleteManyAsync(System.Collections.Generic.IEnumerable<int> ids, System.Threading.CancellationToken ct = default)
        => _inner.DeleteManyAsync(ids, ct);
    }
  }
}
