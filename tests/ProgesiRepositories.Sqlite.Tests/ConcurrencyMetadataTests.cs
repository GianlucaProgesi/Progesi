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
  public class ConcurrencyMetadataTests : IDisposable
  {
    private readonly string _dbPath;
    private readonly SqliteMetadataRepository _repo;

    public ConcurrencyMetadataTests()
    {
      SqliteTestBootstrap.EnsureInitialized();
      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_meta_conc_{Guid.NewGuid():N}.sqlite");
      _repo = new SqliteMetadataRepository(_dbPath, resetSchema: true);
    }

    [Fact]
    public async Task Parallel_Upserts_Do_Not_Create_Duplicates()
    {
      var refs = new[] { new Uri("https://a"), new Uri("https://b") };

      var tasks = Enumerable.Range(0, 8).Select(async i =>
      {
        var m = ProgesiMetadata.Create("usr", "info", refs, id: i + 1);
        m.AddSnip(new byte[] { 1, 2, 3 }, "image/png", "cap");
        await _repo.UpsertAsync(m);
      });

      await Task.WhenAll(tasks);

      var list = await _repo.ListAsync();
      list.Should().HaveCount(1);
      list.Single().CreatedBy.Should().Be("usr");
    }

    public void Dispose()
    {
      try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
  }
}
