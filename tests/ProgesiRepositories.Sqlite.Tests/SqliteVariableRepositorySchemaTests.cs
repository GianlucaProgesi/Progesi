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

namespace ProgesiRepositories.Sqlite.Tests
{
  public sealed class SqliteVariableRepositorySchemaTests : IDisposable
  {
    private readonly string _dbPath;

    public SqliteVariableRepositorySchemaTests()
    {
      SqliteTestBootstrap.EnsureInitialized();
      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_var_schema_{Guid.NewGuid():N}.sqlite");
      _ = new SqliteVariableRepository(_dbPath, resetSchema: true);
    }

    [Fact]
    public void Fresh_variables_table_has_single_unique_content_hash_index_and_not_null_column()
    {
      using var conn = new SqliteConnection($"Data Source={_dbPath}");
      conn.Open();

      var contentHashIndexes = GetContentHashIndexes(conn);
      contentHashIndexes.Should().HaveCount(1);
      contentHashIndexes[0].Name.Should().Be("IX_Variables_ContentHash");
      contentHashIndexes[0].IsUnique.Should().BeTrue();

      using var infoCmd = conn.CreateCommand();
      infoCmd.CommandText = "PRAGMA table_info('Variables');";
      using var reader = infoCmd.ExecuteReader();
      while (reader.Read())
      {
        var name = reader.GetString(1);
        if (!string.Equals(name, "ContentHash", StringComparison.OrdinalIgnoreCase))
          continue;

        reader.GetInt64(3).Should().Be(1, "ContentHash should be NOT NULL on a fresh schema");
        return;
      }

      throw new InvalidOperationException("ContentHash column not found.");
    }

    [Fact]
    public async Task SaveAsync_deduplicates_when_second_row_shares_content_hash()
    {
      var repo = new SqliteVariableRepository(_dbPath, resetSchema: false);
      var first = new ProgesiVariable(1, "A", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 7 });
      await repo.SaveAsync(first);

      var second = new ProgesiVariable(2, "A", 42, new[] { 2, 3, 1 }, metadataIds: new[] { 7 });
      var returned = await repo.SaveAsync(second);

      returned.Id.Should().Be(1);

      using var conn = new SqliteConnection($"Data Source={_dbPath}");
      conn.Open();
      using var countCmd = conn.CreateCommand();
      countCmd.CommandText = "SELECT COUNT(*) FROM Variables WHERE ContentHash=$h;";
      countCmd.Parameters.AddWithValue("$h", ProgesiHash.Compute(first));
      Convert.ToInt32(countCmd.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public void Unique_content_hash_index_rejects_raw_duplicate_insert()
    {
      using var conn = new SqliteConnection($"Data Source={_dbPath}");
      conn.Open();

      using (var insert = conn.CreateCommand())
      {
        insert.CommandText = @"
INSERT INTO Variables (Id, Name, ValueType, Value, MetadataIdsJson, DependsJson, ContentHash)
VALUES (1, 'A', 'int', '1', '[]', '[]', 'dup-hash');";
        insert.ExecuteNonQuery();
      }

      using var duplicate = conn.CreateCommand();
      duplicate.CommandText = @"
INSERT INTO Variables (Id, Name, ValueType, Value, MetadataIdsJson, DependsJson, ContentHash)
VALUES (2, 'B', 'int', '2', '[]', '[]', 'dup-hash');";

      Action act = () => duplicate.ExecuteNonQuery();
      act.Should().Throw<SqliteException>();
    }

    private static IList<(string Name, bool IsUnique)> GetContentHashIndexes(SqliteConnection conn)
    {
      var results = new List<(string Name, bool IsUnique)>();

      using var listCmd = conn.CreateCommand();
      listCmd.CommandText = "PRAGMA index_list('Variables');";
      using var listReader = listCmd.ExecuteReader();
      while (listReader.Read())
      {
        var indexName = listReader.GetString(1);
        var isUnique = listReader.GetInt64(2) == 1;

        using var infoCmd = conn.CreateCommand();
        infoCmd.CommandText = $"PRAGMA index_info('{indexName}');";
        using var infoReader = infoCmd.ExecuteReader();
        while (infoReader.Read())
        {
          var columnName = infoReader.GetString(2);
          if (string.Equals(columnName, "ContentHash", StringComparison.OrdinalIgnoreCase))
            results.Add((indexName, isUnique));
        }
      }

      return results;
    }

    public void Dispose()
    {
      try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
  }
}
