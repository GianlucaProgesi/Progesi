using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ProgesiCore;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Repositories;
using Rhino.Geometry;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class EfVariableRepositoryGeometryTests : IDisposable
{
  private const string ObjectMarkerPrefix = "@OBJECT:";

  private readonly string _connectionString;
  private readonly EfVariableRepository _repo;

  public EfVariableRepositoryGeometryTests()
  {
    _connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    _repo = new EfVariableRepository(_connectionString, resetSchema: true);
  }

  [Fact]
  public async Task SaveAsync_GeometryLikeValue_Persists_ObjectColumns_And_ObjectMarker()
  {
    var geometry = new FakeCurve();
    var original = new ProgesiVariable(101, "Curve", geometry, new[] { 1 }, metadataIds: new[] { 2 });

    await _repo.SaveAsync(original);

    using var ctx = ProgesiDbContextFactory.Create(_connectionString);
    var entity = await ctx.Variables.AsNoTracking().SingleAsync(v => v.Id == 101);

    entity.ObjectType.Should().Be(typeof(FakeCurve).FullName);
    entity.ObjectPayloadJson.Should().NotBeNullOrWhiteSpace();
    entity.Value.Should().StartWith(ObjectMarkerPrefix);
    entity.Value.Should().Be(ObjectMarkerPrefix + entity.ObjectType);
  }

  [Fact]
  public async Task SaveAsync_GeometryLikeValue_ReadBack_Returns_Payload_And_Preserves_Metadata()
  {
    var geometry = new FakeCurve();
    var original = new ProgesiVariable(102, "Curve", geometry, new[] { 3, 4 }, metadataIds: new[] { 5, 6 });

    await _repo.SaveAsync(original);
    var loaded = await _repo.GetByIdAsync(102);

    loaded.Should().NotBeNull();
    loaded!.Value.Should().BeOfType<string>();
    loaded.Value.Should().Be(JsonConvert.SerializeObject(geometry));
    loaded.DependsFrom.Should().BeEquivalentTo(new[] { 3, 4 });
    loaded.MetadataIds.Should().Equal(5, 6);
  }

  [Fact]
  public async Task SaveAsync_GeometryLikeValue_SaveReadResave_Is_HashStable_And_Deduplicates()
  {
    var geometry = new FakeCurve();
    var original = new ProgesiVariable(103, "Curve", geometry, metadataIds: new[] { 8 });
    var payload = JsonConvert.SerializeObject(geometry);

    ProgesiHash.CanonicalValue(geometry).Should().Be(ProgesiHash.CanonicalValue(payload));

    await _repo.SaveAsync(original);
    var loaded = await _repo.GetByIdAsync(103);
    loaded.Should().NotBeNull();

    var resaved = await _repo.SaveAsync(new ProgesiVariable(104, "Curve", loaded!.Value, metadataIds: new[] { 8 }));

    resaved.Id.Should().Be(103);
    ProgesiHash.Compute(resaved).Should().Be(ProgesiHash.Compute(original));
    (await _repo.GetAllAsync()).Select(v => v.Id).Should().Contain(103).And.NotContain(104);
  }

  [Fact]
  public async Task SaveAsync_NonGeometryValue_Leaves_ObjectColumns_Empty()
  {
    var original = new ProgesiVariable(104, "Label", "plain-text", metadataIds: new[] { 1 });

    await _repo.SaveAsync(original);

    using var ctx = ProgesiDbContextFactory.Create(_connectionString);
    var entity = await ctx.Variables.AsNoTracking().SingleAsync(v => v.Id == 104);

    entity.ObjectType.Should().BeEmpty();
    entity.ObjectPayloadJson.Should().BeEmpty();
    entity.Value.Should().Be("plain-text");

    var loaded = await _repo.GetByIdAsync(104);
    loaded.Should().NotBeNull();
    loaded!.Value.Should().Be("plain-text");
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
