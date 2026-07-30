using FluentAssertions;
using ProgesiCore;
using Progesi.Infrastructure.EF.Repositories;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class EfClusterRepositoryTests : IDisposable
{
  private readonly string _connectionString;
  private readonly EfClusterRepository _repo;

  public EfClusterRepositoryTests()
  {
    _connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    _repo = new EfClusterRepository(_connectionString, resetSchema: true);
  }

  [Fact]
  public async Task SaveAsync_Then_GetByIdAsync_RoundTrips_Cluster()
  {
    var cluster = ProgesiVariableCluster.Rehydrate(7, "RepoC", new[] { 2, 1 }, "desc");

    await _repo.SaveAsync(cluster);
    var loaded = await _repo.GetByIdAsync(7);

    loaded.Should().NotBeNull();
    loaded!.Id.Should().Be(7);
    loaded.Name.Should().Be("RepoC");
    loaded.Description.Should().Be("desc");
    loaded.ProgesiVariableIds.Should().Equal(1, 2);
    loaded.Hashtag.Should().Be(cluster.Hashtag);
    ProgesiHash.Compute(loaded).Should().Be(ProgesiHash.Compute(cluster));
  }

  [Fact]
  public async Task GetByHashtagAsync_Finds_Saved_Cluster_By_Current_Hashtag()
  {
    var cluster = ProgesiVariableCluster.Rehydrate(3, "HashC", new[] { 9 }, null);
    await _repo.SaveAsync(cluster);

    var loaded = await _repo.GetByHashtagAsync(cluster.Hashtag);

    loaded.Should().NotBeNull();
    loaded!.Id.Should().Be(3);
  }

  [Fact]
  public async Task GetByHashtagAsync_Finds_Saved_Cluster_By_Legacy_Hashtag()
  {
    var cluster = ProgesiVariableCluster.Rehydrate(5, "LegacyC", new[] { 4, 8 }, "note");
    await _repo.SaveAsync(cluster);

    var legacy = ProgesiVariableCluster.BuildLegacyHashtag(
        cluster.Id,
        cluster.Name,
        cluster.ProgesiVariableIds);

    var loaded = await _repo.GetByHashtagAsync(legacy);

    loaded.Should().NotBeNull();
    loaded!.Id.Should().Be(5);
  }

  [Fact]
  public async Task GetByHashtagAsync_Returns_Null_For_Empty_Or_Whitespace_Hashtag()
  {
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "A", new[] { 1 }, null));

    (await _repo.GetByHashtagAsync("")).Should().BeNull();
    (await _repo.GetByHashtagAsync("   ")).Should().BeNull();
  }

  [Fact]
  public async Task SaveAsync_Deduplicates_By_ContentHash()
  {
    var first = ProgesiVariableCluster.Rehydrate(1, "Dup", new[] { 3, 1, 2 }, "same");
    await _repo.SaveAsync(first);

    var second = ProgesiVariableCluster.Rehydrate(2, "Dup", new[] { 2, 3, 1 }, "same");
    var returned = await _repo.SaveAsync(second);

    returned.Id.Should().Be(1);

    var all = await _repo.GetAllAsync();
    all.Should().HaveCount(1);
    all[0].Id.Should().Be(1);
  }

  [Fact]
  public async Task GetAllAsync_Returns_Clusters_Ordered_By_Id()
  {
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(2, "B", new[] { 2 }, null));
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "A", new[] { 1 }, null));

    var all = await _repo.GetAllAsync();

    all.Should().HaveCount(2);
    all.Select(c => c.Id).Should().Equal(1, 2);
  }

  [Fact]
  public async Task DeleteAsync_Removes_Existing_Cluster()
  {
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(4, "Del", new[] { 1 }, null));

    (await _repo.DeleteAsync(4)).Should().BeTrue();
    (await _repo.GetByIdAsync(4)).Should().BeNull();
  }

  [Fact]
  public async Task DeleteAsync_Returns_False_When_Not_Found()
  {
    (await _repo.DeleteAsync(999)).Should().BeFalse();
  }

  [Fact]
  public async Task DeleteManyAsync_Removes_Only_Existing_Ids()
  {
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "A", new[] { 1 }, null));
    await _repo.SaveAsync(ProgesiVariableCluster.Rehydrate(2, "B", new[] { 2 }, null));

    var removed = await _repo.DeleteManyAsync(new[] { 1, 99, 2 });

    removed.Should().Be(2);
    (await _repo.GetAllAsync()).Should().BeEmpty();
  }

  public void Dispose()
  {
    _repo.Dispose();
    var path = _connectionString.Replace("Data Source=", string.Empty);
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}
