using FluentAssertions;
using ProgesiCore;
using Progesi.Infrastructure.EF.Repositories;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class EfMetadataRepositoryTests : IDisposable
{
  private readonly string _connectionString;
  private readonly EfMetadataRepository _repo;

  public EfMetadataRepositoryTests()
  {
    _connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    _repo = new EfMetadataRepository(_connectionString, resetSchema: true);
  }

  [Fact]
  public async Task ListAsync_EmptyStore_Returns_Empty()
  {
    var all = await _repo.ListAsync();
    all.Should().BeEmpty();
  }

  [Fact]
  public async Task Upsert_Deduplicates_By_ContentHash()
  {
    var r1 = new Uri("https://example.com/A");
    var r2 = new Uri("https://example.com/B");

    var m1 = ProgesiMetadata.Create("usr", "info", new[] { r1, r2 }, id: 1);
    m1.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap");

    await _repo.UpsertAsync(m1);

    var m2 = ProgesiMetadata.Create("usr", "info", new[] { r2, r1 }, id: 2);
    m2.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "caption changes ok");

    await _repo.UpsertAsync(m2);

    var got1 = await _repo.GetAsync(1);
    var got2 = await _repo.GetAsync(2);

    got1.Should().NotBeNull();
    got2.Should().BeNull();
  }

  [Fact]
  public async Task Roundtrip_Metadata_With_Snips_And_References()
  {
    var m = ProgesiMetadata.Create("me", "meta", new[] { new Uri("https://a") }, id: 5);
    m.AddSnip(new byte[] { 9, 9, 9 }, "image/jpeg", "pic");

    await _repo.UpsertAsync(m);
    var back = await _repo.GetAsync(5);

    back.Should().NotBeNull();
    back!.CreatedBy.Should().Be("me");
    back.References.Should().HaveCount(1);
    back.Snips.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetAsync_Missing_Id_Returns_Null()
  {
    var back = await _repo.GetAsync(404);
    back.Should().BeNull();
  }

  [Fact]
  public async Task DeleteAsync_Existing_Id_Removes_Metadata()
  {
    var m = ProgesiMetadata.Create("usr", id: 3);
    await _repo.UpsertAsync(m);

    (await _repo.DeleteAsync(3)).Should().BeTrue();
    (await _repo.GetAsync(3)).Should().BeNull();
    (await _repo.ListAsync()).Should().BeEmpty();
  }

  [Fact]
  public async Task DeleteAsync_Missing_Id_Returns_False()
  {
    (await _repo.DeleteAsync(999)).Should().BeFalse();
  }

  [Fact]
  public async Task ListAsync_Returns_Multiple_Records_In_Order()
  {
    await _repo.UpsertAsync(ProgesiMetadata.Create("a", id: 2));
    await _repo.UpsertAsync(ProgesiMetadata.Create("b", id: 1));

    var list = await _repo.ListAsync();

    list.Should().HaveCount(2);
    list.Select(m => m.Id).Should().Equal(1, 2);
  }

  [Fact]
  public async Task Upsert_Preserves_Hash_Key_Semantics_For_Duplicate_Content()
  {
    var m1 = ProgesiMetadata.Create("same", "payload", id: 10);
    await _repo.UpsertAsync(m1);

    var m2 = ProgesiMetadata.Create("same", "payload", id: 11);
    await _repo.UpsertAsync(m2);

    (await _repo.GetAsync(10)).Should().NotBeNull();
    (await _repo.GetAsync(11)).Should().BeNull();
    (await _repo.ListAsync()).Should().HaveCount(1);
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
