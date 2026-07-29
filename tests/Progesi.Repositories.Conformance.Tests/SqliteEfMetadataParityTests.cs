using FluentAssertions;
using ProgesiCore;
using Progesi.Repositories.Conformance.Tests.Support;

namespace Progesi.Repositories.Conformance.Tests;

public sealed class SqliteEfMetadataParityTests
{
  [Fact]
  public async Task UpsertAndRead_BasicMetadata_Produces_Equivalent_Results()
  {
    using var stores = new SqliteEfMetadataParityStores();
    var original = ProgesiMetadata.Create("author", "notes", id: 4);

    await stores.Sqlite.UpsertAsync(original);
    await stores.Ef.UpsertAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetAsync(4);
    var efLoaded = await stores.Ef.GetAsync(4);

    ParityAssertions.MetadataShouldMatch(sqliteLoaded, efLoaded, original);
  }

  [Fact]
  public async Task UpsertAndRead_Metadata_With_References_And_Snips_Produces_Equivalent_Results()
  {
    using var stores = new SqliteEfMetadataParityStores();
    var snip = ProgesiSnip.Create(new byte[] { 1, 2, 3 }, "image/png", "cap");
    var original = ProgesiMetadata.Create(
      "me",
      "meta",
      new[] { new Uri("https://example.com/a"), new Uri("https://example.com/b") },
      new[] { snip },
      id: 5);

    await stores.Sqlite.UpsertAsync(original);
    await stores.Ef.UpsertAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetAsync(5);
    var efLoaded = await stores.Ef.GetAsync(5);

    ParityAssertions.MetadataShouldMatch(sqliteLoaded, efLoaded, original);
  }

  [Fact]
  public async Task Upsert_Deduplicates_By_ContentHash_With_Identical_Behaviour()
  {
    using var stores = new SqliteEfMetadataParityStores();
    var m1 = ProgesiMetadata.Create("same", "payload", id: 10);
    var m2 = ProgesiMetadata.Create("same", "payload", id: 11);

    await stores.Sqlite.UpsertAsync(m1);
    await stores.Ef.UpsertAsync(m1);
    await stores.Sqlite.UpsertAsync(m2);
    await stores.Ef.UpsertAsync(m2);

    (await stores.Sqlite.GetAsync(10)).Should().NotBeNull();
    (await stores.Ef.GetAsync(10)).Should().NotBeNull();
    (await stores.Sqlite.GetAsync(11)).Should().BeNull();
    (await stores.Ef.GetAsync(11)).Should().BeNull();
  }

  [Fact]
  public async Task GetByHashtagAsync_Returns_Equivalent_Metadata_On_Both_Providers()
  {
    using var stores = new SqliteEfMetadataParityStores();
    var original = ProgesiMetadata.Create("tagged", "info", id: 6);

    await stores.Sqlite.UpsertAsync(original);
    await stores.Ef.UpsertAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByHashtagAsync(original.Hashtag);
    var efLoaded = await stores.Ef.GetByHashtagAsync(original.Hashtag);

    ParityAssertions.MetadataShouldMatch(sqliteLoaded, efLoaded, original);
  }
}
