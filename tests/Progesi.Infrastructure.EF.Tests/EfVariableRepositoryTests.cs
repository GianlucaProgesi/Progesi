using FluentAssertions;
using ProgesiCore;
using Progesi.Infrastructure.EF.Repositories;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class EfVariableRepositoryTests : IDisposable
{
  private readonly string _connectionString;
  private readonly EfVariableRepository _repo;

  public EfVariableRepositoryTests()
  {
    _connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    _repo = new EfVariableRepository(_connectionString, resetSchema: true);
  }

  [Fact]
  public async Task GetAllAsync_EmptyStore_Returns_Empty()
  {
    var all = await _repo.GetAllAsync();
    all.Should().BeEmpty();
  }

  [Fact]
  public async Task SaveAsync_Then_GetByIdAsync_RoundTrips_Fields()
  {
    var original = new ProgesiVariable(5, "Span", 12.5, new[] { 1, 2 }, metadataId: 3);

    await _repo.SaveAsync(original);
    var loaded = await _repo.GetByIdAsync(5);

    loaded.Should().NotBeNull();
    loaded!.Id.Should().Be(5);
    loaded.Name.Should().Be("Span");
    loaded.Value.Should().Be(12.5);
    loaded.DependsFrom.Should().BeEquivalentTo(new[] { 1, 2 });
    loaded.MetadataId.Should().Be(3);
  }

  [Fact]
  public async Task GetByIdAsync_Missing_Id_Returns_Null()
  {
    var loaded = await _repo.GetByIdAsync(999);
    loaded.Should().BeNull();
  }

  [Fact]
  public async Task GetAllAsync_Returns_All_Saved_Variables()
  {
    await _repo.SaveAsync(new ProgesiVariable(1, "A", 1));
    await _repo.SaveAsync(new ProgesiVariable(2, "B", 2));

    var all = await _repo.GetAllAsync();

    all.Should().HaveCount(2);
    all.Select(v => v.Id).Should().BeEquivalentTo(new[] { 1, 2 });
  }

  [Fact]
  public async Task SaveAsync_Overwrite_Same_Id_Replaces_Stored_Variable()
  {
    await _repo.SaveAsync(new ProgesiVariable(7, "Before", 1));
    await _repo.SaveAsync(new ProgesiVariable(7, "After", 9));

    var loaded = await _repo.GetByIdAsync(7);

    loaded.Should().NotBeNull();
    loaded!.Name.Should().Be("After");
    loaded.Value.Should().Be(9);
    (await _repo.GetAllAsync()).Should().HaveCount(1);
  }

  [Fact]
  public async Task SaveAsync_Preserves_Hash_Computable_Content()
  {
    var original = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 }, metadataId: 4);

    await _repo.SaveAsync(original);
    var loaded = await _repo.GetByIdAsync(11);

    loaded.Should().NotBeNull();
    ProgesiHash.Compute(loaded!).Should().Be(ProgesiHash.Compute(original));
  }

  [Fact]
  public async Task Save_Deduplicates_By_ContentHash()
  {
    var v1 = new ProgesiVariable(1, "A", 42, new[] { 3, 1, 2 }, metadataId: 7);
    await _repo.SaveAsync(v1);

    var v2 = new ProgesiVariable(2, "A", 42, new[] { 2, 3, 1 }, metadataId: 7);
    var ret = await _repo.SaveAsync(v2);

    ret.Id.Should().Be(1);

    var all = await _repo.GetAllAsync();
    all.Select(x => x.Id).Should().Contain(1).And.NotContain(2);
  }

  [Fact]
  public async Task DeleteAsync_Existing_Id_Removes_Variable()
  {
    await _repo.SaveAsync(new ProgesiVariable(8, "Del", 0));

    (await _repo.DeleteAsync(8)).Should().BeTrue();
    (await _repo.GetByIdAsync(8)).Should().BeNull();
    (await _repo.GetAllAsync()).Should().BeEmpty();
  }

  [Fact]
  public async Task DeleteAsync_Missing_Id_Returns_False()
  {
    (await _repo.DeleteAsync(404)).Should().BeFalse();
  }

  [Fact]
  public async Task DeleteManyAsync_Removes_Existing_Ids()
  {
    await _repo.SaveAsync(new ProgesiVariable(1, "A", 1));
    await _repo.SaveAsync(new ProgesiVariable(2, "B", 2));
    await _repo.SaveAsync(new ProgesiVariable(3, "C", 3));

    var removed = await _repo.DeleteManyAsync(new[] { 1, 3, 99 });

    removed.Should().Be(2);
    (await _repo.GetAllAsync()).Select(v => v.Id).Should().Equal(2);
  }

  [Fact]
  public async Task DeleteManyAsync_Empty_Ids_Returns_Zero()
  {
    var removed = await _repo.DeleteManyAsync(Array.Empty<int>());
    removed.Should().Be(0);
  }

  public void Dispose()
  {
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
