using FluentAssertions;
using Newtonsoft.Json;
using ProgesiCore;
using Progesi.Repositories.Conformance.Tests.Support;
using Rhino.Geometry;

namespace Progesi.Repositories.Conformance.Tests;

public sealed class SqliteEfVariableParityTests
{
  [Theory]
  [InlineData("text-value")]
  [InlineData(42)]
  [InlineData(3.14)]
  [InlineData(true)]
  public async Task SaveAndRead_NonGeometryValue_Produces_Equivalent_Results(object value)
  {
    using var stores = new SqliteEfVariableParityStores();
    var original = new ProgesiVariable(1, "Field", value, new[] { 2, 1 }, metadataIds: new[] { 5, 9 });

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByIdAsync(1);
    var efLoaded = await stores.Ef.GetByIdAsync(1);

    ParityAssertions.VariablesShouldMatch(sqliteLoaded!, efLoaded!, original);
  }

  [Fact]
  public async Task SaveAndRead_GeometryLikeValue_Produces_Equivalent_Payload_And_Hash()
  {
    using var stores = new SqliteEfVariableParityStores();
    var geometry = new FakeCurve();
    var original = new ProgesiVariable(2, "Curve", geometry, new[] { 4 }, metadataIds: new[] { 7 });

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByIdAsync(2);
    var efLoaded = await stores.Ef.GetByIdAsync(2);

    ParityAssertions.VariablesShouldMatch(sqliteLoaded!, efLoaded!, original);
    sqliteLoaded!.Value.Should().Be(JsonConvert.SerializeObject(geometry));
    efLoaded!.Value.Should().Be(sqliteLoaded.Value);
  }

  [Fact]
  public async Task Save_Deduplicates_ContentDuplicate_With_Identical_Behaviour()
  {
    using var stores = new SqliteEfVariableParityStores();
    var v1 = new ProgesiVariable(1, "A", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 7 });
    var v2 = new ProgesiVariable(2, "A", 42, new[] { 2, 3, 1 }, metadataIds: new[] { 7 });

    await stores.Sqlite.SaveAsync(v1);
    await stores.Ef.SaveAsync(v1);

    var sqliteDeduped = await stores.Sqlite.SaveAsync(v2);
    var efDeduped = await stores.Ef.SaveAsync(v2);

    sqliteDeduped.Id.Should().Be(1);
    efDeduped.Id.Should().Be(1);
    sqliteDeduped.Id.Should().Be(efDeduped.Id);
    sqliteDeduped.Hashtag.Should().Be(efDeduped.Hashtag);

    var sqliteByTag = await stores.Sqlite.GetByHashtagAsync(v1.Hashtag);
    var efByTag = await stores.Ef.GetByHashtagAsync(v1.Hashtag);

    ParityAssertions.VariablesShouldMatch(sqliteByTag!, efByTag!, v1);
  }

  [Fact]
  public async Task GetByHashtagAsync_Returns_Equivalent_Variable_On_Both_Providers()
  {
    using var stores = new SqliteEfVariableParityStores();
    var original = new ProgesiVariable(3, "Tagged", "payload", metadataIds: new[] { 2 });

    await stores.Sqlite.SaveAsync(original);
    await stores.Ef.SaveAsync(original);

    var sqliteLoaded = await stores.Sqlite.GetByHashtagAsync(original.Hashtag);
    var efLoaded = await stores.Ef.GetByHashtagAsync(original.Hashtag);

    ParityAssertions.VariablesShouldMatch(sqliteLoaded!, efLoaded!, original);
  }
}
