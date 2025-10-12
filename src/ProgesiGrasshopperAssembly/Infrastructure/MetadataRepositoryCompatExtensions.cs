// MetadataRepositoryCompatExtensions.cs
#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class MetadataRepositoryCompatExtensions
  {
    // ===== DTO riflessione-friendly =====
    private sealed class LiveMetaDto
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string By { get; set; }
      public string[] Refs { get; set; }
      public string[] Snips { get; set; }
      public string LastModified { get; set; }
    }

    private sealed class LiveVarDto
    {
      public int Id { get; set; }
      public string Hash { get; set; }
      public string Name { get; set; }
      public string Value { get; set; }
      public string Unit { get; set; }
      public string By { get; set; }
      public string LastModified { get; set; }
    }

    // ===== Utility comuni =====
    private static string[] SplitPipe(string s)
    {
      if (string.IsNullOrEmpty(s)) return new string[0];
      var p = s.Split('|');
      for (int i = 0; i < p.Length; i++) p[i] = (p[i] ?? "").Trim();
      return p;
    }

    private static string ReadString(object obj, string name)
    {
      if (obj == null) return "";
      var pi = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return "";
      var v = pi.GetValue(obj, null);
      return v == null ? "" : v.ToString();
    }

    private static int ReadInt(object obj, string name)
    {
      if (obj == null) return 0;
      var pi = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
      if (pi == null) return 0;
      var v = pi.GetValue(obj, null);
      if (v == null) return 0;
      int n;
      return int.TryParse(v.ToString(), out n) ? n : 0;
    }

    private static string IsoNowUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    // ========= METADATA =========

    public static bool TryGetByHashThenId(object repoObj, string hash, int id, out object metadata, out string info)
    {
      metadata = null; info = string.Empty;

      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        if (!System.IO.File.Exists(live.DbPath)) { info = "DB non trovato (live)"; return false; }

        try
        {
          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var cmd = cn.CreateCommand())
            {
              if (!string.IsNullOrWhiteSpace(hash))
              {
                cmd.CommandText = "SELECT Id,Hash,By,Ref,Snips,LastModifiedUtc FROM metadata WHERE Hash=@h LIMIT 1;";
                cmd.Parameters.AddWithValue("@h", hash);
              }
              else if (id > 0)
              {
                cmd.CommandText = "SELECT Id,Hash,By,Ref,Snips,LastModifiedUtc FROM metadata WHERE Id=@i LIMIT 1;";
                cmd.Parameters.AddWithValue("@i", id);
              }
              else { info = "Input non valido (hash/id)."; return false; }

              using (var rd = cmd.ExecuteReader())
              {
                if (!rd.Read()) { info = "Non trovato (live)"; return false; }

                metadata = new LiveMetaDto
                {
                  Id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                  Hash = rd.IsDBNull(1) ? "" : rd.GetString(1),
                  By = rd.IsDBNull(2) ? "" : rd.GetString(2),
                  Refs = SplitPipe(rd.IsDBNull(3) ? "" : rd.GetString(3)),
                  Snips = SplitPipe(rd.IsDBNull(4) ? "" : rd.GetString(4)),
                  LastModified = rd.IsDBNull(5) ? "" : rd.GetString(5)
                };
                info = "OK";
                return true;
              }
            }
          }
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }

      var mock = repoObj as FileMockMetadataRepository;
      if (mock != null)
      {
        if (!string.IsNullOrWhiteSpace(hash))
        {
          if (mock.TryGetByHash(hash, out var dto, out info)) { metadata = dto; return true; }
          return false;
        }
        if (id > 0)
        {
          if (mock.TryGetById(id, out var dto, out info)) { metadata = dto; return true; }
          return false;
        }
        info = "Input non valido (hash/id).";
        return false;
      }

      info = "OK (nessun repo collegato)";
      return false;
    }

    public static bool TryUpsert(object repoObj, object payload, out object persisted, out string info)
    {
      persisted = null; info = "OK";

      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        try
        {
          int id = ReadInt(payload, "id");
          string by = ReadString(payload, "by");
          string rf = ReadString(payload, "rf");
          string sn = ReadString(payload, "sn");
          string nowIso = IsoNowUtc();
          string hash = "mock-" + (id > 0 ? id : 0).ToString("00000000");

          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var tr = cn.BeginTransaction())
            using (var cmd = cn.CreateCommand())
            {
              if (id <= 0)
              {
                cmd.CommandText = "SELECT IFNULL(MAX(Id),0)+1 FROM metadata;";
                id = Convert.ToInt32(cmd.ExecuteScalar());
                hash = "mock-" + id.ToString("00000000");
              }

              cmd.CommandText = @"
INSERT INTO metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc)
VALUES(@id, @hash, @by, @ref, @sn, @lm)
ON CONFLICT(Id) DO UPDATE SET
  Hash=@hash, By=@by, Ref=@ref, Snips=@sn, LastModifiedUtc=@lm;";
              cmd.Parameters.AddWithValue("@id", id);
              cmd.Parameters.AddWithValue("@hash", hash);
              cmd.Parameters.AddWithValue("@by", by ?? "");
              cmd.Parameters.AddWithValue("@ref", rf ?? "");
              cmd.Parameters.AddWithValue("@sn", sn ?? "");
              cmd.Parameters.AddWithValue("@lm", nowIso);
              cmd.ExecuteNonQuery();

              tr.Commit();
            }
          }

          persisted = new LiveMetaDto
          {
            Id = id,
            Hash = hash,
            By = by ?? "",
            Refs = SplitPipe(rf ?? ""),
            Snips = SplitPipe(sn ?? ""),
            LastModified = nowIso
          };
          info = "OK";
          return true;
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }

      // Nessun repo: echo permissivo
      int echoId = ReadInt(payload, "id"); if (echoId <= 0) echoId = 1;
      string echoHash = "mock-" + echoId.ToString("00000000");
      persisted = new LiveMetaDto
      {
        Id = echoId,
        Hash = echoHash,
        By = ReadString(payload, "by"),
        Refs = SplitPipe(ReadString(payload, "rf")),
        Snips = SplitPipe(ReadString(payload, "sn")),
        LastModified = IsoNowUtc()
      };
      info = "OK (nessun repo collegato)";
      return true;
    }

    public static bool TryDelete(object repoObj, int id, out string info)
    {
      info = "OK";
      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        try
        {
          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var cmd = cn.CreateCommand())
            {
              cmd.CommandText = "DELETE FROM metadata WHERE Id=@i;";
              cmd.Parameters.AddWithValue("@i", id);
              var n = cmd.ExecuteNonQuery();
              info = (n > 0) ? "OK" : "Non trovato (live)";
              return n > 0;
            }
          }
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }
      info = "OK (nessun repo collegato)";
      return false;
    }

    // ========= VARIABLES =========

    public static bool TryGetVariableByHashThenId(object repoObj, string hash, int id, out object variable, out string info)
    {
      variable = null; info = string.Empty;

      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        if (!System.IO.File.Exists(live.DbPath)) { info = "DB non trovato (live)"; return false; }

        try
        {
          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var cmd = cn.CreateCommand())
            {
              if (!string.IsNullOrWhiteSpace(hash))
              {
                cmd.CommandText = "SELECT Id,Hash,Name,Value,Unit,By,LastModifiedUtc FROM variables WHERE Hash=@h LIMIT 1;";
                cmd.Parameters.AddWithValue("@h", hash);
              }
              else if (id > 0)
              {
                cmd.CommandText = "SELECT Id,Hash,Name,Value,Unit,By,LastModifiedUtc FROM variables WHERE Id=@i LIMIT 1;";
                cmd.Parameters.AddWithValue("@i", id);
              }
              else { info = "Input non valido (hash/id)."; return false; }

              using (var rd = cmd.ExecuteReader())
              {
                if (!rd.Read()) { info = "Non trovato (live)"; return false; }

                variable = new LiveVarDto
                {
                  Id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                  Hash = rd.IsDBNull(1) ? "" : rd.GetString(1),
                  Name = rd.IsDBNull(2) ? "" : rd.GetString(2),
                  Value = rd.IsDBNull(3) ? "" : rd.GetString(3),
                  Unit = rd.IsDBNull(4) ? "" : rd.GetString(4),
                  By = rd.IsDBNull(5) ? "" : rd.GetString(5),
                  LastModified = rd.IsDBNull(6) ? "" : rd.GetString(6)
                };
                info = "OK";
                return true;
              }
            }
          }
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }

      // MOCK / NONE – echo base
      if (!string.IsNullOrWhiteSpace(hash) || id > 0)
      {
        variable = new LiveVarDto
        {
          Id = id > 0 ? id : 1,
          Hash = string.IsNullOrWhiteSpace(hash) ? ("mock-" + (id > 0 ? id : 1).ToString("00000000")) : hash,
          Name = "",
          Value = "",
          Unit = "",
          By = "",
          LastModified = IsoNowUtc()
        };
        info = "OK (nessun repo collegato)";
        return true;
      }

      info = "Input non valido (hash/id).";
      return false;
    }

    public static bool TryUpsertVariable(object repoObj, object payload, out object persisted, out string info)
    {
      persisted = null; info = "OK";

      string name = ReadString(payload, "name");
      string value = ReadString(payload, "value");
      string unit = ReadString(payload, "unit");
      string by = ReadString(payload, "by");
      int id = ReadInt(payload, "id");

      // >>> FIX: se value e unit sono numerici, applica fattore e azzera Unit (robustezza anche nel ramo LIVE)
      var inv = CultureInfo.InvariantCulture;
      double vFix, uFix;
      if (double.TryParse(value, NumberStyles.Any, inv, out vFix) &&
          double.TryParse(unit, NumberStyles.Any, inv, out uFix))
      {
        var combined = vFix * uFix;
        value = combined.ToString(inv);
        unit = "";
      }

      string nowIso = IsoNowUtc();
      string hash = string.Format(inv, "{0}|{1}|{2}|{3}|{4}|{5}", id, (name ?? "").Trim().ToUpperInvariant(),
                                    value ?? "", (unit ?? "").Trim().ToUpperInvariant(), (by ?? "").Trim().ToUpperInvariant(), nowIso);

      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        try
        {
          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var tr = cn.BeginTransaction())
            using (var cmd = cn.CreateCommand())
            {
              if (id <= 0)
              {
                cmd.CommandText = "SELECT IFNULL(MAX(Id),0)+1 FROM variables;";
                id = Convert.ToInt32(cmd.ExecuteScalar());
                hash = string.Format(inv, "{0}|{1}|{2}|{3}|{4}|{5}", id, (name ?? "").Trim().ToUpperInvariant(),
                                     value ?? "", (unit ?? "").Trim().ToUpperInvariant(), (by ?? "").Trim().ToUpperInvariant(), nowIso);
              }

              cmd.CommandText = @"
INSERT INTO variables(Id,Hash,Name,Value,Unit,By,LastModifiedUtc)
VALUES(@id,@hash,@name,@value,@unit,@by,@lm)
ON CONFLICT(Id) DO UPDATE SET
  Hash=@hash, Name=@name, Value=@value, Unit=@unit, By=@by, LastModifiedUtc=@lm;";
              cmd.Parameters.AddWithValue("@id", id);
              cmd.Parameters.AddWithValue("@hash", hash ?? "");
              cmd.Parameters.AddWithValue("@name", name ?? "");
              cmd.Parameters.AddWithValue("@value", value ?? "");
              cmd.Parameters.AddWithValue("@unit", unit ?? "");
              cmd.Parameters.AddWithValue("@by", by ?? "");
              cmd.Parameters.AddWithValue("@lm", nowIso);
              cmd.ExecuteNonQuery();

              tr.Commit();
            }
          }

          persisted = new LiveVarDto
          {
            Id = id,
            Hash = hash,
            Name = name ?? "",
            Value = value ?? "",
            Unit = unit ?? "",
            By = by ?? "",
            LastModified = nowIso
          };
          info = "OK";
          return true;
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }

      // MOCK / NONE
      int echoId = id > 0 ? id : 1;
      persisted = new LiveVarDto
      {
        Id = echoId,
        Hash = string.Format(inv, "{0}|{1}|{2}|{3}|{4}|{5}", echoId, (name ?? "").Trim().ToUpperInvariant(),
                               value ?? "", (unit ?? "").Trim().ToUpperInvariant(), (by ?? "").Trim().ToUpperInvariant(), nowIso),
        Name = name ?? "",
        Value = value ?? "",
        Unit = unit ?? "",
        By = by ?? "",
        LastModified = nowIso
      };
      info = "OK (nessun repo collegato)";
      return true;
    }

    public static bool TryDeleteVariable(object repoObj, int id, out string info)
    {
      info = "OK";
      var live = repoObj as ServiceHub.LiveSqliteContext;
      if (live != null)
      {
        try
        {
          using (var cn = new SqliteConnection("Data Source=" + live.DbPath))
          {
            cn.Open();
            using (var cmd = cn.CreateCommand())
            {
              cmd.CommandText = "DELETE FROM variables WHERE Id=@i;";
              cmd.Parameters.AddWithValue("@i", id);
              var n = cmd.ExecuteNonQuery();
              info = (n > 0) ? "OK" : "Non trovato (live)";
              return n > 0;
            }
          }
        }
        catch (Exception ex) { info = "Errore live: " + ex.Message; return false; }
      }
      info = "OK (nessun repo collegato)";
      return false;
    }
  }
}
