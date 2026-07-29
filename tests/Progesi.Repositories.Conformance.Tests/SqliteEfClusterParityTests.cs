using FluentAssertions;
using ProgesiCore;
using Progesi.Repositories.Conformance.Tests.Support;

namespace Progesi.Repositories.Conformance.Tests;

public sealed class SqliteEfClusterParityTests
{
  [Fact]
  public async Task SaveAndRead_Cluster_Produces_Equivalent_Results()
  {
    using var stores = new SqliteEfClusterParityStores();
    var original = ProgesiVariableCluster.Rehydrate(7, "ParityC", new[] { 2, 1 }, "desc");

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByIdAsync(7);
    var efLoaded = await stores.Ef.GetByIdAsync(7);

    ParityAssertions.ClustersShouldMatch(sqliteLoaded!, efLoaded!, original);
  }

  [Fact]
  public async Task Save_Deduplicates_ContentDuplicate_With_Identical_Behaviour()
  {
    using var stores = new SqliteEfClusterParityStores();
    var c1 = ProgesiVariableCluster.Rehydrate(1, "Dup", new[] { 3, 1, 2 }, "same");
    var c2 = ProgesiVariableCluster.Rehydrate(2, "Dup", new[] { 2, 3, 1 }, "same");

    await stores.Sqlite.SaveAsync(c1);
    await stores.Ef.SaveAsync(c1);

    var sqliteDeduped = await stores.Sqlite.SaveAsync(c2);
    var efDeduped = await stores.Ef.SaveAsync(c2);

    sqliteDeduped.Id.Should().Be(1);
    efDeduped.Id.Should().Be(1);
    sqliteDeduped.Id.Should().Be(efDeduped.Id);
    sqliteDeduped.Hashtag.Should().Be(efDeduped.Hashtag);

    var sqliteByTag = await stores.Sqlite.GetByHashtagAsync(c1.Hashtag);
    var efByTag = await stores.Ef.GetByHashtagAsync(c1.Hashtag);

    ParityAssertions.ClustersShouldMatch(sqliteByTag!, efByTag!, c1);
  }

  [Fact]
  public async Task GetByHashtagAsync_Returns_Equivalent_Cluster_On_Both_Providers()
  {
    using var stores = new SqliteEfClusterParityStores();
    var original = ProgesiVariableCluster.Rehydrate(3, "Tagged", new[] { 9 }, null);

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByHashtagAsync(original.Hashtag);
    var efLoaded = await stores.Ef.GetByHashtagAsync(original.Hashtag);

    ParityAssertions.ClustersShouldMatch(sqliteLoaded!, efLoaded!, original);
  }
}
