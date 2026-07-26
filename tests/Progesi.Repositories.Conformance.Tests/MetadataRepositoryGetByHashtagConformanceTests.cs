using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace Progesi.Repositories.Conformance.Tests
{
  public class MetadataRepositoryGetByHashtagConformanceTests
  {
    [Fact]
    public async Task Sqlite_GetByHashtagAsync_After_Upsert_Returns_Same_Metadata()
    {
      var dbPath = Path.Combine(Path.GetTempPath(), "progesi-meta-hashtag-" + Guid.NewGuid().ToString("N") + ".db");
      try
      {
        var repo = new SqliteMetadataRepository(dbPath, resetSchema: true);
        var original = ProgesiMetadata.Create("usr", "payload", id: 4);

        await repo.UpsertAsync(original);
        var loaded = await repo.GetByHashtagAsync(original.Hashtag);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(4);
        loaded.Hashtag.Should().Be(original.Hashtag);
      }
      finally
      {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) File.Delete(dbPath);
      }
    }

    [Fact]
    public async Task Sqlite_GetByHashtagAsync_Miss_Returns_Null()
    {
      var dbPath = Path.Combine(Path.GetTempPath(), "progesi-meta-hashtag-" + Guid.NewGuid().ToString("N") + ".db");
      try
      {
        var repo = new SqliteMetadataRepository(dbPath, resetSchema: true);
        (await repo.GetByHashtagAsync("missing")).Should().BeNull();
      }
      finally
      {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) File.Delete(dbPath);
      }
    }

    private sealed class InMemoryMetadataRepository : IMetadataRepository
    {
      private readonly List<ProgesiMetadata> _items = new List<ProgesiMetadata>();

      public Task<ProgesiMetadata?> GetAsync(int id, System.Threading.CancellationToken ct = default)
      {
        foreach (var item in _items)
          if (item.Id == id) return Task.FromResult<ProgesiMetadata?>(item);
        return Task.FromResult<ProgesiMetadata?>(null);
      }

      public Task<ProgesiMetadata?> GetByHashtagAsync(string hashtag, System.Threading.CancellationToken ct = default)
      {
        if (string.IsNullOrWhiteSpace(hashtag))
          return Task.FromResult<ProgesiMetadata?>(null);

        foreach (var item in _items)
          if (string.Equals(item.Hashtag, hashtag, StringComparison.Ordinal))
            return Task.FromResult<ProgesiMetadata?>(item);

        return Task.FromResult<ProgesiMetadata?>(null);
      }

      public Task UpsertAsync(ProgesiMetadata metadata, System.Threading.CancellationToken ct = default)
      {
        for (int i = 0; i < _items.Count; i++)
        {
          if (_items[i].Id == metadata.Id)
          {
            _items[i] = metadata;
            return Task.CompletedTask;
          }
        }
        _items.Add(metadata);
        return Task.CompletedTask;
      }

      public Task<bool> DeleteAsync(int id, System.Threading.CancellationToken ct = default)
      {
        for (int i = 0; i < _items.Count; i++)
        {
          if (_items[i].Id == id)
          {
            _items.RemoveAt(i);
            return Task.FromResult(true);
          }
        }
        return Task.FromResult(false);
      }

      public Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, System.Threading.CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<ProgesiMetadata>)_items.ToArray());
    }

    [Fact]
    public async Task InMemory_GetByHashtagAsync_After_Upsert_Returns_Same_Metadata()
    {
      var repo = new InMemoryMetadataRepository();
      var original = ProgesiMetadata.Create("me", "info", id: 2);

      await repo.UpsertAsync(original);
      var loaded = await repo.GetByHashtagAsync(original.Hashtag);

      loaded.Should().NotBeNull();
      loaded!.CreatedBy.Should().Be("me");
    }
  }
}
