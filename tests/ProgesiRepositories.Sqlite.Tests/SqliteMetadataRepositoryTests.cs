using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public class SqliteMetadataRepositoryTests : IDisposable
  {
    private readonly string _dbPath;
    private readonly SqliteMetadataRepository _repo;

    public SqliteMetadataRepositoryTests()
    {
      SqliteTestBootstrap.EnsureInitialized();

      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_meta_{Guid.NewGuid():N}.sqlite");
      _repo = new SqliteMetadataRepository(_dbPath, resetSchema: true);

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
      got2.Should().BeNull(); // dedup ha evitato la scrittura di id=2
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

    public void Dispose()
    {
      try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
  }
}
