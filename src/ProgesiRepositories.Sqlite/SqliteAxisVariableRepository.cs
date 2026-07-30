#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using ProgesiCore.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
  public sealed class SqliteAxisVariableRepository : SqliteRepositoryBase, IProgesiVariableAxisRepository
  {
    public SqliteAxisVariableRepository(string dbPath, bool resetSchema = false)
        : base(dbPath, resetSchema)
    {
      EnsureSchema();
    }

    public SqliteAxisVariableRepository(string dbPath, bool resetSchema, IProgesiLogger logger)
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
DROP TABLE IF EXISTS Axis;

CREATE TABLE Axis (
    Id              INTEGER PRIMARY KEY,
    AxisName        TEXT NOT NULL DEFAULT '',
    Name            TEXT NOT NULL,
    ValueTypeKey    TEXT NOT NULL,
    AxisLength      REAL NULL,
    CurvePayload    TEXT NOT NULL DEFAULT '',
    Mode            INTEGER NOT NULL DEFAULT 0,
    KeyPointsJson   TEXT NOT NULL DEFAULT '[]',
    RuleId          INTEGER NULL,
    FunctionId      INTEGER NULL,
    FunctionHashtag TEXT NULL,
    FunctionPayload TEXT NOT NULL DEFAULT '',
    StationsJson    TEXT NOT NULL DEFAULT '[]',
    ContentHash     TEXT NOT NULL,
    Hashtag         TEXT NOT NULL DEFAULT ''
);";
          cmd.ExecuteNonQuery();
          _log.Info("[SQLite] Recreated table 'Axis' due to resetSchema=true.");
        }
        else
        {
          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Axis (
    Id              INTEGER PRIMARY KEY,
    AxisName        TEXT NOT NULL DEFAULT '',
    Name            TEXT NOT NULL,
    ValueTypeKey    TEXT NOT NULL,
    AxisLength      REAL NULL,
    CurvePayload    TEXT NOT NULL DEFAULT '',
    Mode            INTEGER NOT NULL DEFAULT 0,
    KeyPointsJson   TEXT NOT NULL DEFAULT '[]',
    RuleId          INTEGER NULL,
    FunctionId      INTEGER NULL,
    FunctionHashtag TEXT NULL,
    FunctionPayload TEXT NOT NULL DEFAULT '',
    StationsJson    TEXT NOT NULL DEFAULT '[]',
    ContentHash     TEXT NOT NULL,
    Hashtag         TEXT NOT NULL DEFAULT ''
);";
          cmd.ExecuteNonQuery();

          AddColumnIfMissing(conn, "Axis", "AxisName", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Axis", "CurvePayload", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Axis", "Mode", "INTEGER NOT NULL DEFAULT 0");
          AddColumnIfMissing(conn, "Axis", "KeyPointsJson", "TEXT NOT NULL DEFAULT '[]'");
          AddColumnIfMissing(conn, "Axis", "FunctionId", "INTEGER NULL");
          AddColumnIfMissing(conn, "Axis", "FunctionHashtag", "TEXT NULL");
          AddColumnIfMissing(conn, "Axis", "FunctionPayload", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Axis", "StationsJson", "TEXT NOT NULL DEFAULT '[]'");
          AddColumnIfMissing(conn, "Axis", "ContentHash", "TEXT NOT NULL DEFAULT ''");
          AddColumnIfMissing(conn, "Axis", "Hashtag", "TEXT NOT NULL DEFAULT ''");
        }
      }

      EnsureSchemaInfoAndCleanup(conn, "Axis");

      using (var idx = conn.CreateCommand())
      {
        idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Axis_Hashtag ON Axis(Hashtag);";
        idx.ExecuteNonQuery();
      }
    }

    public Task<ProgesiAxisVariable> SaveAsync(ProgesiAxisVariable axis, CancellationToken ct = default)
    {
      if (axis is null) throw new ArgumentNullException(nameof(axis));
      return SaveInternalAsync(axis, ct);
    }

    private Task<ProgesiAxisVariable> SaveInternalAsync(ProgesiAxisVariable axis, CancellationToken ct)
    {
      return WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        var hash = ProgesiHash.Compute(axis);
        var dto = AxisPersistenceMapping.FromDomain(axis);
        var stationsJson = AxisPersistenceMapping.SerializeStations(dto.Entries);
        var keyPointsJson = JsonConvert.SerializeObject(dto.KeyPoints ?? new List<double>());
        var hashtag = axis.Hashtag ?? string.Empty;

        int? existingId = null;
        using (var find = conn.CreateCommand())
        {
          find.Transaction = tx;
          find.CommandText = "SELECT Id FROM Axis WHERE ContentHash=$h LIMIT 1;";
          find.Parameters.AddWithValue("$h", hash);
          var obj = await find.ExecuteScalarAsync(ct);
          if (obj != null && obj != DBNull.Value)
            existingId = Convert.ToInt32(obj);
        }

        if (existingId.HasValue && existingId.Value != axis.Id)
        {
          tx.Commit();
          _log.Debug($"[SQLite] Axis upsert dedup: reused Id={existingId.Value} for ContentHash={hash}.");
          return (await GetByIdAsync(existingId.Value, ct))!;
        }

        using (var cmd = conn.CreateCommand())
        {
          cmd.Transaction = tx;
          cmd.CommandText = @"
INSERT INTO Axis (
  Id, AxisName, Name, ValueTypeKey, AxisLength, CurvePayload, Mode, KeyPointsJson,
  RuleId, FunctionId, FunctionHashtag, FunctionPayload, StationsJson, ContentHash, Hashtag)
VALUES (
  $id, $axisName, $name, $valueTypeKey, $axisLength, $curvePayload, $mode, $keyPointsJson,
  $ruleId, $functionId, $functionHashtag, $functionPayload, $stationsJson, $h, $tag)
ON CONFLICT(Id) DO UPDATE SET
  AxisName=excluded.AxisName,
  Name=excluded.Name,
  ValueTypeKey=excluded.ValueTypeKey,
  AxisLength=excluded.AxisLength,
  CurvePayload=excluded.CurvePayload,
  Mode=excluded.Mode,
  KeyPointsJson=excluded.KeyPointsJson,
  RuleId=excluded.RuleId,
  FunctionId=excluded.FunctionId,
  FunctionHashtag=excluded.FunctionHashtag,
  FunctionPayload=excluded.FunctionPayload,
  StationsJson=excluded.StationsJson,
  ContentHash=excluded.ContentHash,
  Hashtag=excluded.Hashtag;";
          cmd.Parameters.AddWithValue("$id", axis.Id);
          cmd.Parameters.AddWithValue("$axisName", dto.AxisName ?? string.Empty);
          cmd.Parameters.AddWithValue("$name", dto.Name ?? string.Empty);
          cmd.Parameters.AddWithValue("$valueTypeKey", dto.ValueTypeKey ?? string.Empty);
          cmd.Parameters.AddWithValue("$axisLength", (object?)dto.AxisLength ?? DBNull.Value);
          cmd.Parameters.AddWithValue("$curvePayload", dto.CurvePayload ?? string.Empty);
          cmd.Parameters.AddWithValue("$mode", (int)dto.Mode);
          cmd.Parameters.AddWithValue("$keyPointsJson", keyPointsJson);
          cmd.Parameters.AddWithValue("$ruleId", (object?)dto.RuleId ?? DBNull.Value);
          cmd.Parameters.AddWithValue("$functionId", (object?)dto.FunctionId ?? DBNull.Value);
          cmd.Parameters.AddWithValue("$functionHashtag", (object?)dto.FunctionHashtag ?? DBNull.Value);
          cmd.Parameters.AddWithValue("$functionPayload", dto.FunctionPayload ?? string.Empty);
          cmd.Parameters.AddWithValue("$stationsJson", stationsJson);
          cmd.Parameters.AddWithValue("$h", hash);
          cmd.Parameters.AddWithValue("$tag", hashtag);
          await cmd.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
        return (await GetByIdAsync(axis.Id, ct))!;
      }, ct: ct);
    }

    public async Task<ProgesiAxisVariable?> GetByIdAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {AxisPersistenceMapping.SelectColumns} FROM Axis WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
          return null;

        var dto = AxisPersistenceMapping.ReadDto(r);
        return AxisPersistenceMapping.ToDomain(dto);
      }, ct: ct);
    }

    public async Task<ProgesiAxisVariable?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();

        int? id = null;
        using (var cmd = conn.CreateCommand())
        {
          cmd.CommandText = "SELECT Id FROM Axis WHERE Hashtag=$h LIMIT 1;";
          cmd.Parameters.AddWithValue("$h", hashtag);
          var scalar = await cmd.ExecuteScalarAsync(ct);
          if (scalar != null && scalar != DBNull.Value)
            id = Convert.ToInt32(scalar);
        }

        if (!id.HasValue)
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = "SELECT Id FROM Axis WHERE ContentHash=$h LIMIT 1;";
          cmd.Parameters.AddWithValue("$h", hashtag);
          var scalar = await cmd.ExecuteScalarAsync(ct);
          if (scalar != null && scalar != DBNull.Value)
            id = Convert.ToInt32(scalar);
        }

        if (!id.HasValue)
          return null;

        return await GetByIdAsync(id.Value, ct);
      }, ct: ct);
    }

    public async Task<IReadOnlyList<ProgesiAxisVariable>> GetAllAsync(CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        var list = new List<ProgesiAxisVariable>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Axis ORDER BY Id;";

        using var r = await cmd.ExecuteReaderAsync(ct);
        var ids = new List<int>();
        while (await r.ReadAsync(ct))
          ids.Add(r.GetInt32(0));

        foreach (var id in ids)
        {
          var axis = await GetByIdAsync(id, ct);
          if (axis != null)
            list.Add(axis);
        }

        return (IReadOnlyList<ProgesiAxisVariable>)list;
      }, ct: ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Axis WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        return n > 0;
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
          cmd.CommandText = "DELETE FROM Axis WHERE Id=$id;";
          cmd.Parameters.AddWithValue("$id", id);
          count += await cmd.ExecuteNonQueryAsync(ct);
        }
        tx.Commit();
        return count;
      }, ct: ct);
    }
  }
}
