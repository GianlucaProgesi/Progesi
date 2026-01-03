#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using Progesi.Core.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
  /// <summary>
  /// Repository SQLite per ProgesiVariableCluster.
  /// Implementa IProgesiVariableClusterRepository usando lo stesso pattern
  /// già usato per SqliteVariableRepository / SqliteMetadataRepository.
  /// </summary>
  public sealed class SqliteVariableClusterRepository : SqliteRepositoryBase, IProgesiVariableClusterRepository
  {
    // Costruttore "semplice" (senza logger esplicito)
    public SqliteVariableClusterRepository(string dbPath, bool resetSchema = false)
      : base(dbPath, resetSchema)
    {
      EnsureSchema();
    }

    // Costruttore con logger iniettabile
    public SqliteVariableClusterRepository(string dbPath, bool resetSchema, IProgesiLogger logger)
      : base(dbPath, resetSchema, logger)
    {
      EnsureSchema();
    }

    /// <summary>
    /// Crea/aggiorna la tabella VariableClusters e garantisce la presenza di ContentHash.
    /// </summary>
    private void EnsureSchema()
    {
      using var conn = OpenConnection();
      using (var cmd = conn.CreateCommand())
      {
        if (_resetSchema)
        {
          cmd.CommandText = @"
DROP TABLE IF EXISTS VariableClusters;

CREATE TABLE VariableClusters (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    Description  TEXT NULL,
    VariableIds  TEXT NOT NULL,
    Hashtag      TEXT NOT NULL,
    ContentHash  TEXT
);";
          cmd.ExecuteNonQuery();
        }
        else
        {
          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS VariableClusters (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    Description  TEXT NULL,
    VariableIds  TEXT NOT NULL,
    Hashtag      TEXT NOT NULL,
    ContentHash  TEXT
);";
          cmd.ExecuteNonQuery();

          // per DB legacy che non avessero ContentHash
          AddColumnIfMissing(conn, "VariableClusters", "ContentHash", "TEXT");
        }
      }

      // Colonna + indice UNIQUE(ContentHash)
      EnsureContentHash(conn, "VariableClusters");
    }

    // ========================= CRUD =========================

    public Task<ProgesiVariableCluster> SaveAsync(ProgesiVariableCluster cluster, CancellationToken ct = default)
      => SaveInternalAsync(cluster, ct);

    private async Task<ProgesiVariableCluster> SaveInternalAsync(ProgesiVariableCluster cluster, CancellationToken ct)
    {
      if (cluster is null) throw new ArgumentNullException(nameof(cluster));

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        var hash = ProgesiHash.Compute(cluster);

        int? existingId = null;
        using (var find = conn.CreateCommand())
        {
          find.Transaction = tx;
          find.CommandText = "SELECT Id FROM VariableClusters WHERE ContentHash=$h LIMIT 1;";
          find.Parameters.AddWithValue("$h", hash);
          var obj = await find.ExecuteScalarAsync(ct);
          if (obj != null && obj != DBNull.Value)
            existingId = Convert.ToInt32(obj);
        }

        // dedup per ContentHash: se esiste già un cluster identico, lo riutilizziamo
        if (existingId.HasValue && existingId.Value != cluster.Id)
        {
          tx.Commit();
          _log.Debug($"[SQLite] Cluster upsert dedup: reused Id={existingId.Value} for ContentHash={hash}.");

          var existing = await GetByIdAsync(existingId.Value, ct);
          if (existing == null)
            throw new InvalidOperationException($"ContentHash dedup found Id={existingId.Value} but record not readable.");

          return existing;
        }

        var ids = (cluster.ProgesiVariableIds ?? Array.Empty<int>()).ToArray();
        var idsJson = JsonConvert.SerializeObject(ids);

        using (var cmd = conn.CreateCommand())
        {
          cmd.Transaction = tx;
          cmd.CommandText = @"
INSERT INTO VariableClusters (Id, Name, Description, VariableIds, Hashtag, ContentHash)
VALUES ($id, $name, $desc, $vids, $ht, $h)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  Description=excluded.Description,
  VariableIds=excluded.VariableIds,
  Hashtag=excluded.Hashtag,
  ContentHash=excluded.ContentHash;";
          cmd.Parameters.AddWithValue("$id", cluster.Id);
          cmd.Parameters.AddWithValue("$name", cluster.Name ?? string.Empty);
          if (string.IsNullOrWhiteSpace(cluster.Description))
            cmd.Parameters.AddWithValue("$desc", DBNull.Value);
          else
            cmd.Parameters.AddWithValue("$desc", cluster.Description);
          cmd.Parameters.AddWithValue("$vids", idsJson);
          cmd.Parameters.AddWithValue("$ht", cluster.Hashtag ?? string.Empty);
          cmd.Parameters.AddWithValue("$h", hash);

          var n = await cmd.ExecuteNonQueryAsync(ct);
          _log.Debug($"[SQLite] Cluster upsert insert/update: Id={cluster.Id}, rows affected={n}.");
        }

        tx.Commit();

        var back = await GetByIdAsync(cluster.Id, ct);
        if (back == null)
          throw new InvalidOperationException($"Cluster with Id={cluster.Id} not found immediately after save.");

        return back;
      }, ct: ct);
    }

    public async Task<ProgesiVariableCluster?> GetByIdAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, VariableIds, Hashtag FROM VariableClusters WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
          _log.Debug($"[SQLite] Cluster get Id={id}: not found.");
          return null;
        }

        var cid = r.GetInt32(0);
        var name = r.GetString(1);
        var desc = r.IsDBNull(2) ? null : r.GetString(2);
        var vidsJson = r.GetString(3);
        var hashtag = r.GetString(4);

        var ids = JsonConvert.DeserializeObject<int[]>(vidsJson) ?? Array.Empty<int>();

        var domain = ProgesiVariableCluster.Rehydrate(cid, name, ids, desc, hashtag);
        _log.Debug($"[SQLite] Cluster get Id={id}: hit.");
        return domain;
      }, ct: ct);
    }

    public async Task<ProgesiVariableCluster?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, VariableIds, Hashtag FROM VariableClusters WHERE Hashtag=$h LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", hashtag);

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
          _log.Debug($"[SQLite] Cluster get hashtag={hashtag}: not found.");
          return null;
        }

        var cid = r.GetInt32(0);
        var name = r.GetString(1);
        var desc = r.IsDBNull(2) ? null : r.GetString(2);
        var vidsJson = r.GetString(3);
        var ht = r.GetString(4);

        var ids = JsonConvert.DeserializeObject<int[]>(vidsJson) ?? Array.Empty<int>();

        var domain = ProgesiVariableCluster.Rehydrate(cid, name, ids, desc, ht);
        _log.Debug($"[SQLite] Cluster get hashtag={hashtag}: hit.");
        return domain;
      }, ct: ct);
    }

    public async Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        var list = new List<ProgesiVariableCluster>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM VariableClusters ORDER BY Id;";

        using var r = await cmd.ExecuteReaderAsync(ct);
        var ids = new List<int>();
        while (await r.ReadAsync(ct)) ids.Add(r.GetInt32(0));

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
        cmd.CommandText = "DELETE FROM VariableClusters WHERE Id=$id;";
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
          cmd.CommandText = "DELETE FROM VariableClusters WHERE Id=$id;";
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
