#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
  public sealed class SqliteClusterRepository : SqliteRepositoryBase, IProgesiVariableClusterRepository
  {
    public SqliteClusterRepository(string dbPath, bool resetSchema = false)
        : base(dbPath, resetSchema)
    {
      EnsureSchema();
    }

    public SqliteClusterRepository(string dbPath, bool resetSchema, IProgesiLogger logger)
        : base(dbPath, resetSchema, logger)
    {
      EnsureSchema();
    }

    private void EnsureSchema()
    {
      using var conn = OpenConnection();
      using (var cmd = conn.CreateCommand())
      {
        if (_resetSchema)
        {
          cmd.CommandText = @"
DROP TABLE IF EXISTS Clusters;

CREATE TABLE Clusters (
    Id              INTEGER PRIMARY KEY,
    Name            TEXT NOT NULL,
    Description     TEXT NOT NULL DEFAULT '',
    VariableIdsJson TEXT NOT NULL DEFAULT '[]',
    ContentHash     TEXT NOT NULL,
    Hashtag         TEXT NOT NULL DEFAULT ''
);";
          cmd.ExecuteNonQuery();
          _log.Info("[SQLite] Recreated table 'Clusters' due to resetSchema=true.");
        }
        else
        {
          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Clusters (
    Id              INTEGER PRIMARY KEY,
    Name            TEXT NOT NULL,
    Description     TEXT NOT NULL DEFAULT '',
    VariableIdsJson TEXT NOT NULL DEFAULT '[]',
    ContentHash     TEXT NOT NULL,
    Hashtag         TEXT NOT NULL DEFAULT ''
);";
          cmd.ExecuteNonQuery();

          AddColumnIfMissing(conn, "Clusters", "Description", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Clusters", "VariableIdsJson", "TEXT NOT NULL DEFAULT '[]'");
          AddColumnIfMissing(conn, "Clusters", "ContentHash", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Clusters", "Hashtag", "TEXT NOT NULL DEFAULT ''");
        }
      }

      EnsureSchemaInfoAndCleanup(conn, "Clusters");

      using (var idx = conn.CreateCommand())
      {
        idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Clusters_Hashtag ON Clusters(Hashtag);";
        idx.ExecuteNonQuery();
      }
    }

    public Task<ProgesiVariableCluster> SaveAsync(ProgesiVariableCluster cluster, CancellationToken ct = default)
        => SaveInternalAsync(cluster, ct);

    private Task<ProgesiVariableCluster> SaveInternalAsync(ProgesiVariableCluster cluster, CancellationToken ct)
    {
      if (cluster is null) throw new ArgumentNullException(nameof(cluster));

      return WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        var hash = ProgesiHash.Compute(cluster);

        int? existingId = null;
        using (var find = conn.CreateCommand())
        {
          find.Transaction = tx;
          find.CommandText = "SELECT Id FROM Clusters WHERE ContentHash=$h LIMIT 1;";
          find.Parameters.AddWithValue("$h", hash);
          var obj = await find.ExecuteScalarAsync(ct);
          if (obj != null && obj != DBNull.Value)
            existingId = Convert.ToInt32(obj);
        }

        if (existingId.HasValue && existingId.Value != cluster.Id)
        {
          tx.Commit();
          _log.Debug($"[SQLite] Cluster upsert dedup: reused Id={existingId.Value} for ContentHash={hash}.");
          return (await GetByIdAsync(existingId.Value, ct))!;
        }

        var variableIdsJson = JsonConvert.SerializeObject(cluster.ProgesiVariableIds.ToArray());
        var hashtag = cluster.Hashtag ?? string.Empty;

        using (var cmd = conn.CreateCommand())
        {
          cmd.Transaction = tx;
          cmd.CommandText = @"
INSERT INTO Clusters (Id, Name, Description, VariableIdsJson, ContentHash, Hashtag)
VALUES ($id, $name, $desc, $vids, $h, $tag)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  Description=excluded.Description,
  VariableIdsJson=excluded.VariableIdsJson,
  ContentHash=excluded.ContentHash,
  Hashtag=excluded.Hashtag;";
          cmd.Parameters.AddWithValue("$id", cluster.Id);
          cmd.Parameters.AddWithValue("$name", cluster.Name ?? string.Empty);
          cmd.Parameters.AddWithValue("$desc", cluster.Description ?? string.Empty);
          cmd.Parameters.AddWithValue("$vids", variableIdsJson);
          cmd.Parameters.AddWithValue("$h", hash);
          cmd.Parameters.AddWithValue("$tag", hashtag);
          var n = await cmd.ExecuteNonQueryAsync(ct);
          _log.Debug($"[SQLite] Cluster upsert insert/update: Id={cluster.Id}, rows affected={n}.");
        }

        tx.Commit();
        return (await GetByIdAsync(cluster.Id, ct))!;
      }, ct: ct);
    }

    public async Task<ProgesiVariableCluster?> GetByIdAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, Name, Description, VariableIdsJson, Hashtag
FROM Clusters WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
          _log.Debug($"[SQLite] Cluster get Id={id}: not found.");
          return null;
        }

        var clusterId = r.GetInt32(0);
        var name = r.GetString(1);
        var description = r.IsDBNull(2) ? string.Empty : r.GetString(2);
        var variableIdsJson = r.IsDBNull(3) ? "[]" : r.GetString(3);
        var hashtag = r.IsDBNull(4) ? null : r.GetString(4);

        var ids = JsonConvert.DeserializeObject<int[]>(variableIdsJson) ?? Array.Empty<int>();
        _log.Debug($"[SQLite] Cluster get Id={id}: hit.");
        return ProgesiVariableCluster.Rehydrate(clusterId, name, ids, description, hashtag);
      }, ct: ct);
    }

    public async Task<ProgesiVariableCluster?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();

        int? id = null;
        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandText = "SELECT Id FROM Clusters WHERE Hashtag=$h LIMIT 1;";
          cmd.Parameters.AddWithValue("$h", hashtag);
          var scalar = await cmd.ExecuteScalarAsync(ct);
          if (scalar != null && scalar != DBNull.Value)
            id = Convert.ToInt32(scalar);
        }

        if (!id.HasValue)
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = "SELECT Id, Name, VariableIdsJson FROM Clusters ORDER BY Id;";
          using var r = await cmd.ExecuteReaderAsync(ct);
          while (await r.ReadAsync(ct))
          {
            var rowId = r.GetInt32(0);
            var name = r.GetString(1);
            var variableIdsJson = r.IsDBNull(2) ? "[]" : r.GetString(2);
            var ids = JsonConvert.DeserializeObject<int[]>(variableIdsJson) ?? Array.Empty<int>();
            var legacy = ProgesiVariableCluster.BuildLegacyHashtag(rowId, name, ids);
            if (string.Equals(legacy, hashtag, StringComparison.Ordinal))
            {
              id = rowId;
              break;
            }
          }
        }

        if (!id.HasValue)
        {
          _log.Debug("[SQLite] Cluster get by hashtag: miss.");
          return null;
        }

        return await GetByIdAsync(id.Value, ct);
      }, ct: ct);
    }

    public async Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        var list = new List<ProgesiVariableCluster>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Clusters ORDER BY Id;";

        using var r = await cmd.ExecuteReaderAsync(ct);
        var ids = new List<int>();
        while (await r.ReadAsync(ct))
          ids.Add(r.GetInt32(0));

        foreach (var id in ids)
        {
          var cluster = await GetByIdAsync(id, ct);
          if (cluster != null)
            list.Add(cluster);
        }

        _log.Debug($"[SQLite] Cluster list count={list.Count}.");
        return (IReadOnlyList<ProgesiVariableCluster>)list;
      }, ct: ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Clusters WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        var ok = n > 0;
        _log.Debug($"[SQLite] Cluster delete Id={id}: {(ok ? "deleted" : "not found")}.");
        return ok;
      }, ct: ct);
    }

    public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
    {
      if (idsToDelete == null) return 0;
      var ids = idsToDelete.ToArray();
      if (ids.Length == 0) return 0;

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        int count = 0;
        foreach (var id in ids)
        {
          using var cmd = conn.CreateCommand();
          cmd.Transaction = tx;
          cmd.CommandText = "DELETE FROM Clusters WHERE Id=$id;";
          cmd.Parameters.AddWithValue("$id", id);
          count += await cmd.ExecuteNonQueryAsync(ct);
        }
        tx.Commit();
        _log.Debug($"[SQLite] Cluster delete-many count={count} (requested={ids.Length}).");
        return count;
      }, ct: ct);
    }
  }
}
