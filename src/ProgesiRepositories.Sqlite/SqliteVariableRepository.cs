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
  public sealed class SqliteVariableRepository : SqliteRepositoryBase, IVariableRepository
  {
    // Costruttore esistente
    public SqliteVariableRepository(string dbPath, bool resetSchema = false) : base(dbPath, resetSchema)
    {
      EnsureSchema();
    }

    // ⬇️ NUOVO: costruttore con logger iniettabile
    public SqliteVariableRepository(string dbPath, bool resetSchema, IProgesiLogger logger)
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
DROP TABLE IF EXISTS Variables;

CREATE TABLE Variables (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    ValueType    TEXT NOT NULL,
    Value        TEXT NOT NULL,
    MetadataId   INTEGER NULL,
    MetadataIdsJson TEXT NOT NULL DEFAULT '[]',
    DependsJson  TEXT NOT NULL,
    ContentHash  TEXT,
    ObjectType   TEXT NOT NULL DEFAULT '',
    ObjectPayloadJson TEXT NOT NULL DEFAULT ''
);";
          cmd.ExecuteNonQuery();
          _log.Info("[SQLite] Recreated table 'Variables' due to resetSchema=true.");
        }
        else
        {
          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Variables (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    ValueType    TEXT NOT NULL,
    Value        TEXT NOT NULL,
    MetadataId   INTEGER NULL,
    MetadataIdsJson TEXT NOT NULL DEFAULT '[]',
    DependsJson  TEXT NOT NULL,
    ContentHash  TEXT
);";
          cmd.ExecuteNonQuery();

          // per DB legacy che non avessero ContentHash
          AddColumnIfMissing(conn, "Variables", "ContentHash", "TEXT");
          AddColumnIfMissing(conn, "Variables", "MetadataIdsJson", "TEXT NOT NULL DEFAULT '[]'");
          AddColumnIfMissing(conn, "Variables", "ObjectType", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Variables", "ObjectPayloadJson", "TEXT NOT NULL DEFAULT ''");
        }
      }
      EnsureContentHash(conn, "Variables");

      using (var idx = conn.CreateCommand())
      {
        idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Variables_ContentHash ON Variables(ContentHash);";
        idx.ExecuteNonQuery();
      }
    }

    public Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default)
        => SaveInternalAsync(variable, ct);

    private async Task<ProgesiVariable> SaveInternalAsync(ProgesiVariable v, CancellationToken ct)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        var hash = ProgesiHash.Compute(v);

        int? existingId = null;
        using (var find = conn.CreateCommand())
        {
          find.Transaction = tx;
          find.CommandText = "SELECT Id FROM Variables WHERE ContentHash=$h LIMIT 1;";
          find.Parameters.AddWithValue("$h", hash);
          var obj = await find.ExecuteScalarAsync(ct);
          if (obj != null && obj != DBNull.Value)
            existingId = Convert.ToInt32(obj);
        }

        if (existingId.HasValue && existingId.Value != v.Id)
        {
          tx.Commit();
          _log.Debug($"[SQLite] Variable upsert dedup: reused Id={existingId.Value} for ContentHash={hash}.");
#nullable disable
          return await GetByIdAsync(existingId.Value, ct);
#nullable enable
        }

        var depends = (v.DependsFrom ?? Array.Empty<int>()).ToArray();
        var payloadDepends = JsonConvert.SerializeObject(depends);
        var metadataIds = v.MetadataIds ?? Array.Empty<int>();
        var payloadMetadataIds = JsonConvert.SerializeObject(metadataIds);

        string valueType = TypeOf(v.Value);
        string storedValue;
        string objectType = string.Empty;
        string objectPayloadJson = string.Empty;

        if (IsGeometryLike(v.Value))
        {
          objectType = GeometryTypeName(v.Value!);
          objectPayloadJson = Stringify(v.Value);
          storedValue = BuildObjectMarker(objectType);
        }
        else
        {
          storedValue = Stringify(v.Value);
        }

        using (var cmd = conn.CreateCommand())
        {
          cmd.Transaction = tx;
          cmd.CommandText = @"
INSERT INTO Variables (Id, Name, ValueType, Value, MetadataId, MetadataIdsJson, DependsJson, ContentHash, ObjectType, ObjectPayloadJson)
VALUES ($id, $name, $vt, $val, $mid, $mids, $dep, $h, $otype, $opayload)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  ValueType=excluded.ValueType,
  Value=excluded.Value,
  MetadataId=excluded.MetadataId,
  MetadataIdsJson=excluded.MetadataIdsJson,
  DependsJson=excluded.DependsJson,
  ContentHash=excluded.ContentHash,
  ObjectType=excluded.ObjectType,
  ObjectPayloadJson=excluded.ObjectPayloadJson;";
          cmd.Parameters.AddWithValue("$id", v.Id);
          cmd.Parameters.AddWithValue("$name", v.Name ?? string.Empty);
          cmd.Parameters.AddWithValue("$vt", valueType);
          cmd.Parameters.AddWithValue("$val", storedValue);
          if (v.MetadataId.HasValue) cmd.Parameters.AddWithValue("$mid", v.MetadataId.Value);
          else cmd.Parameters.AddWithValue("$mid", DBNull.Value);
          cmd.Parameters.AddWithValue("$mids", payloadMetadataIds);
          cmd.Parameters.AddWithValue("$dep", payloadDepends);
          cmd.Parameters.AddWithValue("$h", hash);
          cmd.Parameters.AddWithValue("$otype", objectType);
          cmd.Parameters.AddWithValue("$opayload", objectPayloadJson);
          var n = await cmd.ExecuteNonQueryAsync(ct);
          _log.Debug($"[SQLite] Variable upsert insert/update: Id={v.Id}, rows affected={n}.");
        }

        tx.Commit();
#nullable disable
        var back = await GetByIdAsync(v.Id, ct);
#nullable enable
        return back;
      }, ct: ct);
    }

    public async Task<ProgesiVariable?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Variables WHERE ContentHash=$h LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", hashtag);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        if (scalar is null || scalar is DBNull)
        {
          _log.Debug($"[SQLite] Variable get by hashtag: miss.");
          return null;
        }

        var id = Convert.ToInt32(scalar);
#nullable disable
        return await GetByIdAsync(id, ct);
#nullable enable
      }, ct: ct);
    }

#nullable disable
    public async Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, ValueType, Value, MetadataId, MetadataIdsJson, DependsJson, ObjectType, ObjectPayloadJson FROM Variables WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
          _log.Debug($"[SQLite] Variable get Id={id}: not found.");
          return null;
        }

        var vid = r.GetInt32(0);
        var name = r.GetString(1);
        var vType = r.GetString(2);
        var valStr = r.GetString(3);
        int? mid = r.IsDBNull(4) ? (int?)null : r.GetInt32(4);
        var metadataIdsJson = r.IsDBNull(5) ? "[]" : r.GetString(5);
        var depJs = r.IsDBNull(6) ? "[]" : r.GetString(6);
        _ = r.IsDBNull(7) ? string.Empty : r.GetString(7);
        var objectPayloadJson = r.IsDBNull(8) ? string.Empty : r.GetString(8);
        var depends = JsonConvert.DeserializeObject<int[]>(depJs) ?? Array.Empty<int>();
        var metadataIds = ReadMetadataIds(metadataIdsJson, mid);

        object? value;
        if (IsObjectMarker(valStr) && !string.IsNullOrEmpty(objectPayloadJson))
          value = objectPayloadJson;
        else
          value = ParseValue(valStr, vType);
        _log.Debug($"[SQLite] Variable get Id={id}: hit.");
        return new ProgesiVariable(vid, name, value ?? "", depends, metadataIds);
      }, ct: ct);
    }

    private static int[] ReadMetadataIds(string? metadataIdsJson, int? metadataId)
    {
      if (!string.IsNullOrWhiteSpace(metadataIdsJson))
      {
        var parsed = JsonConvert.DeserializeObject<int[]>(metadataIdsJson);
        if (parsed != null && parsed.Length > 0)
          return parsed;
      }

      if (metadataId.HasValue && metadataId.Value > 0)
        return new[] { metadataId.Value };

      return Array.Empty<int>();
    }
#nullable enable

    public async Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        var list = new List<ProgesiVariable>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Variables ORDER BY Id;";

        using var r = await cmd.ExecuteReaderAsync(ct);
        var ids = new List<int>();
        while (await r.ReadAsync(ct)) ids.Add(r.GetInt32(0));

        foreach (var id in ids)
        {
#nullable disable
          var v = await GetByIdAsync(id, ct);
#nullable enable
          if (v != null) list.Add(v);
        }

        _log.Debug($"[SQLite] Variable list count={list.Count}.");
        return (IReadOnlyList<ProgesiVariable>)list;
      }, ct: ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Variables WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        var ok = n > 0;
        _log.Debug($"[SQLite] Variable delete Id={id}: {(ok ? "deleted" : "not found")}.");
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
          cmd.CommandText = "DELETE FROM Variables WHERE Id=$id;";
          cmd.Parameters.AddWithValue("$id", id);
          count += await cmd.ExecuteNonQueryAsync(ct);
        }
        tx.Commit();
        _log.Debug($"[SQLite] Variable delete-many count={count} (requested={ids.Length}).");
        return count;
      }, ct: ct);
    }
  }
}
