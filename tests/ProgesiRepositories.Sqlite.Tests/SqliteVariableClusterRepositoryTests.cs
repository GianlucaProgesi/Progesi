#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Progesi.Core.Variables;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public class SqliteVariableClusterRepositoryTests
  {
    private static void TryDeleteFileBestEffort(string path)
    {
      if (!File.Exists(path)) return;

      // SQLite può trattenere il lock un attimo: retry leggero.
      for (int i = 0; i < 10; i++)
      {
        try
        {
          File.Delete(path);
          return;
        }
        catch (IOException)
        {
          System.Threading.Thread.Sleep(50);
        }
        catch (UnauthorizedAccessException)
        {
          System.Threading.Thread.Sleep(50);
        }
      }

      // Se non ci riesce, pazienza: NON facciamo fallire il test.
    }

    private static string NewTempDbPath()
    {
      var dir = Path.Combine(Path.GetTempPath(), "ProgesiTests");
      Directory.CreateDirectory(dir);
      return Path.Combine(dir, $"clusters_{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task SaveAndGetById_RoundTrip_Works()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        var c1 = ProgesiVariableCluster.Rehydrate(
          id: 1,
          name: "C1",
          progesiVariableIds: new[] { 3, 1, 2 },
          description: "desc",
          hashtagFromStore: null);

        var saved = await repo.SaveAsync(c1);
        Assert.Equal(1, saved.Id);

        var read = await repo.GetByIdAsync(1);
        Assert.NotNull(read);
        Assert.Equal("C1", read!.Name);
        Assert.True(read.ProgesiVariableIds.SequenceEqual(new[] { 1, 2, 3 }));
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }

    [Fact]
    public async Task GetByHashtag_Works()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        var c1 = ProgesiVariableCluster.Rehydrate(
          id: 1,
          name: "C1",
          progesiVariableIds: new[] { 1, 2 },
          description: "desc",
          hashtagFromStore: null);

        var saved = await repo.SaveAsync(c1);

        var read = await repo.GetByHashtagAsync(saved.Hashtag);
        Assert.NotNull(read);
        Assert.Equal(saved.Id, read!.Id);
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }

    [Fact]
    public async Task Save_Deduplicates_By_ContentHash()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        // stesso contenuto logico, Id diversi
        var a = ProgesiVariableCluster.Rehydrate(
          id: 1,
          name: "C1",
          progesiVariableIds: new[] { 1, 2, 3 },
          description: "desc",
          hashtagFromStore: null);

        var b = ProgesiVariableCluster.Rehydrate(
          id: 99,
          name: "C1",
          progesiVariableIds: new[] { 3, 2, 1 },
          description: "desc",
          hashtagFromStore: null);

        var sa = await repo.SaveAsync(a);
        var sb = await repo.SaveAsync(b);

        // per dedup ci aspettiamo che riusi l'Id già presente (quello di a)
        Assert.Equal(sa.Id, sb.Id);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }

    [Fact]
    public async Task Save_With_SameId_Overwrites_Record()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        var c1 = ProgesiVariableCluster.Rehydrate(
          id: 1, name: "C1", progesiVariableIds: new[] { 1, 2 }, description: "desc", hashtagFromStore: null);

        await repo.SaveAsync(c1);

        var c1Updated = ProgesiVariableCluster.Rehydrate(
          id: 1, name: "C1-Updated", progesiVariableIds: new[] { 1, 2, 3 }, description: "desc2", hashtagFromStore: null);

        await repo.SaveAsync(c1Updated);

        var read = await repo.GetByIdAsync(1);
        Assert.NotNull(read);
        Assert.Equal("C1-Updated", read!.Name);
        Assert.True(read.ProgesiVariableIds.SequenceEqual(new[] { 1, 2, 3 }));
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }

    [Fact]
    public async Task Delete_Works()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        var c1 = ProgesiVariableCluster.Rehydrate(
          id: 1, name: "C1", progesiVariableIds: new[] { 1 }, description: "", hashtagFromStore: null);

        await repo.SaveAsync(c1);

        var deleted = await repo.DeleteAsync(1);
        Assert.True(deleted);

        var read = await repo.GetByIdAsync(1);
        Assert.Null(read);
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }

    [Fact]
    public async Task DeleteMany_Works()
    {
      var dbPath = NewTempDbPath();
      try
      {
        var repo = new SqliteVariableClusterRepository(dbPath, resetSchema: true);

        await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(1, "C1", new[] { 1 }, "", null));
        await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(2, "C2", new[] { 2 }, "", null));
        await repo.SaveAsync(ProgesiVariableCluster.Rehydrate(3, "C3", new[] { 3 }, "", null));

        var n = await repo.DeleteManyAsync(new[] { 1, 3 });
        Assert.Equal(2, n);

        Assert.Null(await repo.GetByIdAsync(1));
        Assert.NotNull(await repo.GetByIdAsync(2));
        Assert.Null(await repo.GetByIdAsync(3));
      }
      finally
      {
        TryDeleteFileBestEffort(dbPath);
      }
    }
  }
}
