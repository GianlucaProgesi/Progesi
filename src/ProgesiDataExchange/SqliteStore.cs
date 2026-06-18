using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Progesi.DataExchange
{
  public sealed class SqliteStore : IProgesiStore, IDisposable
  {
    readonly string _db;
    readonly SqliteConnection _cn;
    bool _disposed;

    public SqliteStore(string dbPath, bool createIfMissing)
    {
      if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("DbPath vuoto.");
      _db = Path.GetFullPath(dbPath);
      var dir = Path.GetDirectoryName(_db);
      if (!File.Exists(_db))
      {
        if (!createIfMissing) throw new FileNotFoundException($"SQLite non trovato: {_db}", _db);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using (File.Create(_db)) { }
      }
      _cn = new SqliteConnection($"Data Source={_db};");
      _cn.Open();
      EnsureSchema();
    }

    public void Dispose() { if (_disposed) return; _cn.Dispose(); _disposed = true; }

    void Exec(string sql) { using var cmd = _cn.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }

    void ExecIgnore(string sql)
    {
      try { Exec(sql); }
      catch { /* ignore schema drift */ }
    }
    void EnsureSchema()
    {
      // ATTENZIONE: vecchie versioni usavano "Values" (riservata). Ora: ValueTypeKey + VariableHashes.

      Exec(@"
CREATE TABLE IF NOT EXISTS variables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, Value TEXT, Unit TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS metadata (
  Id TEXT PRIMARY KEY, Hash TEXT, Info TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS axisvariables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, ValueTypeKey TEXT, Unit TEXT, AxisRef TEXT, Stations TEXT, VariableHashes TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE INDEX IF NOT EXISTS idx_variables_hash ON variables(Hash);
CREATE INDEX IF NOT EXISTS idx_metadata_hash ON metadata(Hash);
CREATE INDEX IF NOT EXISTS idx_axis_hash ON axisvariables(Hash);
");

      // Back-compat / migrazione leggera: aggiungi colonne se il DB esiste già con lo schema vecchio
      ExecIgnore(@"ALTER TABLE axisvariables ADD COLUMN ValueTypeKey TEXT;");
      ExecIgnore(@"ALTER TABLE axisvariables ADD COLUMN VariableHashes TEXT;");

      Exec(@"
CREATE TRIGGER IF NOT EXISTS trg_v_lastmod_set AFTER INSERT ON variables
FOR EACH ROW WHEN NEW.LastModifiedUtc IS NULL
BEGIN UPDATE variables SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;
CREATE TRIGGER IF NOT EXISTS trg_v_lastmod_u AFTER UPDATE ON variables
FOR EACH ROW BEGIN UPDATE variables SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;

CREATE TRIGGER IF NOT EXISTS trg_m_lastmod_set AFTER INSERT ON metadata
FOR EACH ROW WHEN NEW.LastModifiedUtc IS NULL
BEGIN UPDATE metadata SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;
CREATE TRIGGER IF NOT EXISTS trg_m_lastmod_u AFTER UPDATE ON metadata
FOR EACH ROW BEGIN UPDATE metadata SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;

CREATE TRIGGER IF NOT EXISTS trg_a_lastmod_set AFTER INSERT ON axisvariables
FOR EACH ROW WHEN NEW.LastModifiedUtc IS NULL
BEGIN UPDATE axisvariables SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;
CREATE TRIGGER IF NOT EXISTS trg_a_lastmod_u AFTER UPDATE ON axisvariables
FOR EACH ROW BEGIN UPDATE axisvariables SET LastModifiedUtc=strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid=NEW.rowid; END;
");
    }

    static string S(object v) => v == null ? "" : v.ToString();

    public IReadOnlyList<ProgesiVariableDto> GetAllVariables()
    {
      var list = new List<ProgesiVariableDto>();
      using var c = _cn.CreateCommand();
      c.CommandText = "SELECT Id,Hash,Name,Value,Unit,By,Ref,LastModifiedUtc FROM variables";
      using var r = c.ExecuteReader();
      while (r.Read()) list.Add(new ProgesiVariableDto
      {
        Id = S(r["Id"]),
        Hash = S(r["Hash"]),
        Name = S(r["Name"]),
        Value = S(r["Value"]),
        Unit = S(r["Unit"]),
        By = S(r["By"]),
        Ref = S(r["Ref"]),
        LastModifiedUtc = S(r["LastModifiedUtc"])
      });
      return list;
    }

    public IReadOnlyList<ProgesiMetadataDto> GetAllMetadata()
    {
      var list = new List<ProgesiMetadataDto>();
      using var c = _cn.CreateCommand();
      c.CommandText = "SELECT Id,Hash,Info,By,Ref,LastModifiedUtc FROM metadata";
      using var r = c.ExecuteReader();
      while (r.Read()) list.Add(new ProgesiMetadataDto
      {
        Id = S(r["Id"]),
        Hash = S(r["Hash"]),
        Info = S(r["Info"]),
        By = S(r["By"]),
        Ref = S(r["Ref"]),
        LastModifiedUtc = S(r["LastModifiedUtc"])
      });
      return list;
    }

    public IReadOnlyList<ProgesiAxisVariableDto> GetAllAxisVariables()
    {
      var list = new List<ProgesiAxisVariableDto>();
      using var c = _cn.CreateCommand();
      c.CommandText = @"SELECT Id,Hash,Name,ValueTypeKey,Unit,AxisRef,Stations,VariableHashes,By,Ref,LastModifiedUtc FROM axisvariables";
      using var r = c.ExecuteReader();
      while (r.Read()) list.Add(new ProgesiAxisVariableDto
      {
        Id = S(r["Id"]),
        Hash = S(r["Hash"]),
        Name = S(r["Name"]),
        Unit = S(r["Unit"]),
        AxisRef = S(r["AxisRef"]),
        Stations = S(r["Stations"]),
        ValueTypeKey = S(r["ValueTypeKey"]),
        VariableHashes = S(r["VariableHashes"]),
        By = S(r["By"]),
        Ref = S(r["Ref"]),
        LastModifiedUtc = S(r["LastModifiedUtc"])
      });
      return list;
    }

    public (int inserted, int updated, int skipped) UpsertVariables(IEnumerable<ProgesiVariableDto> xs)
    {
      int ins = 0, upd = 0, skip = 0;
      foreach (var it in xs)
      {
        bool exists = false; string oldHash = null;
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = "SELECT Hash FROM variables WHERE Id=@id LIMIT 1";
          c.Parameters.Add(new SqliteParameter("@id", it.Id ?? "")); var v = c.ExecuteScalar();
          if (v != null) { exists = true; oldHash = v.ToString(); }
        }
        if (exists && oldHash == it.Hash) { skip++; continue; }
        if (exists && oldHash != it.Hash) it.Id = Guid.NewGuid().ToString("D");
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = "INSERT OR REPLACE INTO variables (Id,Hash,Name,Value,Unit,By,Ref,LastModifiedUtc) VALUES (@Id,@Hash,@Name,@Value,@Unit,@By,@Ref,@Last)";
          c.Parameters.Add(new SqliteParameter("@Id", it.Id ?? Guid.NewGuid().ToString("D")));
          c.Parameters.Add(new SqliteParameter("@Hash", it.Hash ?? ""));
          c.Parameters.Add(new SqliteParameter("@Name", it.Name ?? ""));
          c.Parameters.Add(new SqliteParameter("@Value", it.Value ?? ""));
          c.Parameters.Add(new SqliteParameter("@Unit", it.Unit ?? ""));
          c.Parameters.Add(new SqliteParameter("@By", it.By ?? ""));
          c.Parameters.Add(new SqliteParameter("@Ref", it.Ref ?? ""));
          c.Parameters.Add(new SqliteParameter("@Last", string.IsNullOrWhiteSpace(it.LastModifiedUtc) ? DateTime.UtcNow.ToString("s") + "Z" : it.LastModifiedUtc));
          var n = c.ExecuteNonQuery(); if (n > 0) ins++; else skip++;
        }
      }
      return (ins, upd, skip);
    }

    public (int inserted, int updated, int skipped) UpsertMetadata(IEnumerable<ProgesiMetadataDto> xs)
    {
      int ins = 0, upd = 0, skip = 0;
      foreach (var it in xs)
      {
        bool exists = false; string oldHash = null;
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = "SELECT Hash FROM metadata WHERE Id=@id LIMIT 1";
          c.Parameters.Add(new SqliteParameter("@id", it.Id ?? "")); var v = c.ExecuteScalar();
          if (v != null) { exists = true; oldHash = v.ToString(); }
        }
        if (exists && oldHash == it.Hash) { skip++; continue; }
        if (exists && oldHash != it.Hash) it.Id = Guid.NewGuid().ToString("D");
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = "INSERT OR REPLACE INTO metadata (Id,Hash,Info,By,Ref,LastModifiedUtc) VALUES (@Id,@Hash,@Info,@By,@Ref,@Last)";
          c.Parameters.Add(new SqliteParameter("@Id", it.Id ?? Guid.NewGuid().ToString("D")));
          c.Parameters.Add(new SqliteParameter("@Hash", it.Hash ?? ""));
          c.Parameters.Add(new SqliteParameter("@Info", it.Info ?? ""));
          c.Parameters.Add(new SqliteParameter("@By", it.By ?? ""));
          c.Parameters.Add(new SqliteParameter("@Ref", it.Ref ?? ""));
          c.Parameters.Add(new SqliteParameter("@Last", string.IsNullOrWhiteSpace(it.LastModifiedUtc) ? DateTime.UtcNow.ToString("s") + "Z" : it.LastModifiedUtc));
          var n = c.ExecuteNonQuery(); if (n > 0) ins++; else skip++;
        }
      }
      return (ins, upd, skip);
    }

    public (int inserted, int updated, int skipped) UpsertAxisVariables(IEnumerable<ProgesiAxisVariableDto> xs)
    {
      int ins = 0, upd = 0, skip = 0;
      foreach (var it in xs)
      {
        bool exists = false; string oldHash = null;
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = @"SELECT Hash FROM axisvariables WHERE Id=@id LIMIT 1";
          c.Parameters.Add(new SqliteParameter("@id", it.Id ?? "")); var v = c.ExecuteScalar();
          if (v != null) { exists = true; oldHash = v.ToString(); }
        }
        if (exists && oldHash == it.Hash) { skip++; continue; }
        if (exists && oldHash != it.Hash) it.Id = Guid.NewGuid().ToString("D");
        using (var c = _cn.CreateCommand())
        {
          c.CommandText = @"INSERT OR REPLACE INTO axisvariables
            (Id,Hash,Name,ValueTypeKey,Unit,AxisRef,Stations,VariableHashes,By,Ref,LastModifiedUtc)
            VALUES (@Id,@Hash,@Name,@ValueTypeKey,@Unit,@AxisRef,@Stations,@VariableHashes,@By,@Ref,@Last)";
          c.Parameters.Add(new SqliteParameter("@Id", it.Id ?? Guid.NewGuid().ToString("D")));
          c.Parameters.Add(new SqliteParameter("@Hash", it.Hash ?? ""));
          c.Parameters.Add(new SqliteParameter("@Name", it.Name ?? ""));
          c.Parameters.Add(new SqliteParameter("@ValueTypeKey", it.ValueTypeKey ?? ""));
          c.Parameters.Add(new SqliteParameter("@Unit", it.Unit ?? ""));
          c.Parameters.Add(new SqliteParameter("@AxisRef", it.AxisRef ?? ""));
          c.Parameters.Add(new SqliteParameter("@Stations", it.Stations ?? ""));
          c.Parameters.Add(new SqliteParameter("@VariableHashes", it.VariableHashes ?? ""));
          c.Parameters.Add(new SqliteParameter("@By", it.By ?? ""));
          c.Parameters.Add(new SqliteParameter("@Ref", it.Ref ?? ""));
          c.Parameters.Add(new SqliteParameter("@Last", string.IsNullOrWhiteSpace(it.LastModifiedUtc) ? DateTime.UtcNow.ToString("s") + "Z" : it.LastModifiedUtc));
          var n = c.ExecuteNonQuery(); if (n > 0) ins++; else skip++;
        }
      }
      return (ins, upd, skip);
    }
  }
}
