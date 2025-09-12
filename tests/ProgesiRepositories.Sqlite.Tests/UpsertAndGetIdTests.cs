using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public class UpsertAndGetIdTests
  {
    private static string NewDbPath()
    {
      var dir = Path.Combine(Path.GetTempPath(), "progesi_tests");
      Directory.CreateDirectory(dir);
      return Path.Combine(dir, $"progesi_{Guid.NewGuid():N}.sqlite");
    }

    private static ProgesiSnip Snip() =>
        ProgesiSnip.Create(new byte[] { 1, 2, 3 }, "image/png", "cap", null);

    private static ProgesiMetadata NewMetadata(int? id = null) =>
        ProgesiMetadata.Create("usr", "info",
            new[] { new Uri("https://a/"), new Uri("https://b/") },
            new[] { Snip() },
            DateTime.UtcNow,
            id);

    [Fact]
    public async Task UpsertAndGetId_Returns_Same_Id_For_Duplicate_Content()
    {
      var db = NewDbPath();
      var repo = new SqliteMetadataRepository(db, resetSchema: true);

      var m1 = NewMetadata(id: 10);
      var id1 = await repo.UpsertAndGetIdAsync(m1);
      Assert.Equal(10, id1);

      // stesso contenuto (anche se l'id cambia), deve restituire SEMPRE 10
      var m2 = NewMetadata(id: 999);
      var id2 = await repo.UpsertAndGetIdAsync(m2);

      Assert.Equal(10, id2);

      var all = await repo.ListAsync();
      Assert.Single(all);
      Assert.Equal(10, all.Single().Id);
    }

    [Fact]
    public async Task UpsertAndGetId_Assigns_New_Id_For_New_Content()
    {
      var db = NewDbPath();
      var repo = new SqliteMetadataRepository(db, resetSchema: true);

      var m1 = NewMetadata(id: 1);
      var id1 = await repo.UpsertAndGetIdAsync(m1);
      Assert.Equal(1, id1);

      // **Contenuto diverso**: cambiamo i BYTE dello snip (non solo il caption!)
      var snip2 = ProgesiSnip.Create(new byte[] { 1, 2, 4 }, "image/png", "cap", null);
      var m2 = ProgesiMetadata.Create("usr", "info",
          new[] { new Uri("https://a/"), new Uri("https://b/") },
          new[] { snip2 },
          DateTime.UtcNow,
          id: 2);

      var id2 = await repo.UpsertAndGetIdAsync(m2);
      Assert.Equal(2, id2);

      var all = await repo.ListAsync();
      Assert.Equal(2, all.Count);
    }
  }
}
