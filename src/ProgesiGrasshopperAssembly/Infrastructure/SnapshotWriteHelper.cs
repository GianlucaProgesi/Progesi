// SnapshotWriteHelper.cs
// Progesi – supporto locale: snapshot dall'ultimo Read e write-through verso DB/XLSX
// Dipendenze: Microsoft.Data.Sqlite (>= 9.0.0), ClosedXML (>= 0.104.0)

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ClosedXML.Excel;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class SnapshotWriteHelper
  {
    // ---------- SNAPSHOT ----------
    private static List<object> _lastVars = new List<object>();
    private static List<object> _lastMetas = new List<object>();
    private static List<object> _lastAxis = new List<object>();

    /// <summary>
    /// Copia nello snapshot i dati presenti nello store (chiamare alla fine del Read).
    /// </summary>
    internal static void SaveSnapshotFromStore(object store)
    {
      if (store == null) { _lastVars.Clear(); _lastMetas.Clear(); _lastAxis.Clear(); return; }

      _lastVars = Enumerate(store, "Variables").ToList();
      _lastMetas = Enumerate(store, "Metadata").ToList();
      _lastAxis = Enumerate(store, "AxisVars", "AxisVariables", "Axis").ToList();
    }

    /// <summary>
    /// Restituisce le sorgenti per il Write: usa lo store se ha dati, altrimenti lo snapshot.
    /// </summary>
    internal static (IReadOnlyList<object> vars, IReadOnlyList<object> metas, IReadOnlyList<object> axis)
        GetSources(object store)
    {
      var v = Enumerate(store, "Variables").ToList();
      var m = Enumerate(store, "Metadata").ToList();
      var a = Enumerate(store, "AxisVars", "AxisVariables", "Axis").ToList();

      if (v.Count == 0) v = _lastVars;
      if (m.Count == 0) m = _lastMetas;
      if (a.Count == 0) a = _lastAxis;

      return (v, m, a);
    }

    // ---------- WRITE: SQLITE ----------
    internal static (int insV, int insM, int insA) WriteSqlite(string dbPath,
        IEnumerable<object> vars, IEnumerable<object> metas, IEnumerable<object> axis)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
      using var con = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
      con.Open();

      using (var cmd = con.CreateCommand())
      {
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS variables (
  Id INTEGER PRIMARY KEY,
  Hash TEXT, Name TEXT, Value TEXT, Unit TEXT,
  By TEXT, LM TEXT, LastModifiedUtc TEXT, Info TEXT
);
CREATE TABLE IF NOT EXISTS metadata (
  Id INTEGER PRIMARY KEY,
  Hash TEXT, By TEXT,
  Ref TEXT, Refs TEXT,
  Snip TEXT, Snips TEXT,
  LM TEXT, LastModifiedUtc TEXT, Info TEXT
);";
        cmd.ExecuteNonQuery();
      }

      int insV = 0, insM = 0, insA = 0;

      // variables
      using (var tx = con.BeginTransaction())
      using (var cmd = con.CreateCommand())
      {
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT OR REPLACE INTO variables
(Id,Hash,Name,Value,Unit,By,LM,LastModifiedUtc,Info)
VALUES ($Id,$Hash,$Name,$Value,$Unit,$By,$LM,$LM,$Info);";
        var pId = cmd.CreateParameter(); pId.ParameterName = "$Id"; cmd.Parameters.Add(pId);
        var pHash = cmd.CreateParameter(); pHash.ParameterName = "$Hash"; cmd.Parameters.Add(pHash);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$Name"; cmd.Parameters.Add(pName);
        var pVal = cmd.CreateParameter(); pVal.ParameterName = "$Value"; cmd.Parameters.Add(pVal);
        var pUnit = cmd.CreateParameter(); pUnit.ParameterName = "$Unit"; cmd.Parameters.Add(pUnit);
        var pBy = cmd.CreateParameter(); pBy.ParameterName = "$By"; cmd.Parameters.Add(pBy);
        var pLM = cmd.CreateParameter(); pLM.ParameterName = "$LM"; cmd.Parameters.Add(pLM);
        var pInfo = cmd.CreateParameter(); pInfo.ParameterName = "$Info"; cmd.Parameters.Add(pInfo);

        foreach (var v in vars ?? Enumerable.Empty<object>())
        {
          pId.Value = GetInt(v, "Id");
          pHash.Value = GetStr(v, "Hash");
          pName.Value = GetStr(v, "Name");
          pVal.Value = GetStr(v, "Value");
          pUnit.Value = GetStr(v, "Unit");
          pBy.Value = GetStr(v, "By");
          pLM.Value = GetStr(v, "LM", "LastModifiedUtc") ?? NowIso();
          pInfo.Value = GetStr(v, "Info");
          insV += cmd.ExecuteNonQuery();
        }
        tx.Commit();
      }

      // metadata
      using (var tx = con.BeginTransaction())
      using (var cmd = con.CreateCommand())
      {
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT OR REPLACE INTO metadata
(Id,Hash,By,Ref,Refs,Snip,Snips,LM,LastModifiedUtc,Info)
VALUES ($Id,$Hash,$By,$Ref,$Refs,$Snip,$Snips,$LM,$LM,$Info);";
        var pId = cmd.CreateParameter(); pId.ParameterName = "$Id"; cmd.Parameters.Add(pId);
        var pHash = cmd.CreateParameter(); pHash.ParameterName = "$Hash"; cmd.Parameters.Add(pHash);
        var pBy = cmd.CreateParameter(); pBy.ParameterName = "$By"; cmd.Parameters.Add(pBy);
        var pRef = cmd.CreateParameter(); pRef.ParameterName = "$Ref"; cmd.Parameters.Add(pRef);
        var pRefs = cmd.CreateParameter(); pRefs.ParameterName = "$Refs"; cmd.Parameters.Add(pRefs);
        var pSnip = cmd.CreateParameter(); pSnip.ParameterName = "$Snip"; cmd.Parameters.Add(pSnip);
        var pSnps = cmd.CreateParameter(); pSnps.ParameterName = "$Snips"; cmd.Parameters.Add(pSnps);
        var pLM = cmd.CreateParameter(); pLM.ParameterName = "$LM"; cmd.Parameters.Add(pLM);
        var pInfo = cmd.CreateParameter(); pInfo.ParameterName = "$Info"; cmd.Parameters.Add(pInfo);

        foreach (var m in metas ?? Enumerable.Empty<object>())
        {
          pId.Value = GetInt(m, "Id");
          pHash.Value = GetStr(m, "Hash");
          pBy.Value = GetStr(m, "By");
          var refStr = GetStr(m, "Ref");
          var refsStr = GetStr(m, "Refs");
          pRef.Value = !string.IsNullOrWhiteSpace(refStr) ? refStr : FirstOf(refsStr);
          pRefs.Value = refsStr;
          pSnip.Value = GetStr(m, "Snip");
          pSnps.Value = GetStr(m, "Snips");
          pLM.Value = GetStr(m, "LM", "LastModifiedUtc") ?? NowIso();
          pInfo.Value = GetStr(m, "Info");
          insM += cmd.ExecuteNonQuery();
        }
        tx.Commit();
      }

      // axis (se presenti – stessa struttura delle variables)
      if (axis != null && axis.Any())
      {
        using (var tx = con.BeginTransaction())
        using (var cmd = con.CreateCommand())
        {
          cmd.Transaction = tx;
          cmd.CommandText = @"CREATE TABLE IF NOT EXISTS axisvariables (
  Id INTEGER PRIMARY KEY,
  Hash TEXT, Name TEXT, Value TEXT, Unit TEXT, By TEXT, LM TEXT, LastModifiedUtc TEXT, Info TEXT
);";
          cmd.ExecuteNonQuery();
          cmd.CommandText = @"INSERT OR REPLACE INTO axisvariables
(Id,Hash,Name,Value,Unit,By,LM,LastModifiedUtc,Info)
VALUES ($Id,$Hash,$Name,$Value,$Unit,$By,$LM,$LM,$Info);";

          var pId = cmd.CreateParameter(); pId.ParameterName = "$Id"; cmd.Parameters.Add(pId);
          var pHash = cmd.CreateParameter(); pHash.ParameterName = "$Hash"; cmd.Parameters.Add(pHash);
          var pName = cmd.CreateParameter(); pName.ParameterName = "$Name"; cmd.Parameters.Add(pName);
          var pVal = cmd.CreateParameter(); pVal.ParameterName = "$Value"; cmd.Parameters.Add(pVal);
          var pUnit = cmd.CreateParameter(); pUnit.ParameterName = "$Unit"; cmd.Parameters.Add(pUnit);
          var pBy = cmd.CreateParameter(); pBy.ParameterName = "$By"; cmd.Parameters.Add(pBy);
          var pLM = cmd.CreateParameter(); pLM.ParameterName = "$LM"; cmd.Parameters.Add(pLM);
          var pInfo = cmd.CreateParameter(); pInfo.ParameterName = "$Info"; cmd.Parameters.Add(pInfo);

          foreach (var a in axis)
          {
            pId.Value = GetInt(a, "Id");
            pHash.Value = GetStr(a, "Hash");
            pName.Value = GetStr(a, "Name");
            pVal.Value = GetStr(a, "Value");
            pUnit.Value = GetStr(a, "Unit");
            pBy.Value = GetStr(a, "By");
            pLM.Value = GetStr(a, "LM", "LastModifiedUtc") ?? NowIso();
            pInfo.Value = GetStr(a, "Info");
            insA += cmd.ExecuteNonQuery();
          }
          tx.Commit();
        }
      }

      return (insV, insM, insA);
    }

    // ---------- WRITE: EXCEL (ClosedXML) ----------
    internal static int WriteXlsx(string xlsxPath,
        IEnumerable<object> vars, IEnumerable<object> metas, IEnumerable<object> axis)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(xlsxPath)) ?? ".");
      using var wb = new XLWorkbook();

      // Variables
      var wsV = wb.AddWorksheet("ProgesiVariable");
      WriteHeader(wsV, new[] { "Id", "Hash", "Name", "Value", "Unit", "By", "LM", "Info" });
      int r = 2, rows = 0;
      foreach (var v in vars ?? Enumerable.Empty<object>())
      {
        wsV.Cell(r, 1).SetValue(GetInt(v, "Id"));
        wsV.Cell(r, 2).SetValue(GetStr(v, "Hash"));
        wsV.Cell(r, 3).SetValue(GetStr(v, "Name"));
        wsV.Cell(r, 4).SetValue(GetStr(v, "Value"));
        wsV.Cell(r, 5).SetValue(GetStr(v, "Unit"));
        wsV.Cell(r, 6).SetValue(GetStr(v, "By"));
        wsV.Cell(r, 7).SetValue(GetStr(v, "LM", "LastModifiedUtc") ?? NowIso());
        wsV.Cell(r, 8).SetValue(GetStr(v, "Info"));
        r++; rows++;
      }

      // Metadata
      var wsM = wb.AddWorksheet("ProgesiMetadata");
      WriteHeader(wsM, new[] { "Id", "Hash", "By", "Ref", "Refs", "Snip", "Snips", "LM", "Info" });
      r = 2;
      foreach (var m in metas ?? Enumerable.Empty<object>())
      {
        wsM.Cell(r, 1).SetValue(GetInt(m, "Id"));
        wsM.Cell(r, 2).SetValue(GetStr(m, "Hash"));
        wsM.Cell(r, 3).SetValue(GetStr(m, "By"));
        var refStr = GetStr(m, "Ref");
        var refsStr = GetStr(m, "Refs");
        wsM.Cell(r, 4).SetValue(!string.IsNullOrWhiteSpace(refStr) ? refStr : FirstOf(refsStr));
        wsM.Cell(r, 5).SetValue(refsStr);
        wsM.Cell(r, 6).SetValue(GetStr(m, "Snip"));
        wsM.Cell(r, 7).SetValue(GetStr(m, "Snips"));
        wsM.Cell(r, 8).SetValue(GetStr(m, "LM", "LastModifiedUtc") ?? NowIso());
        wsM.Cell(r, 9).SetValue(GetStr(m, "Info"));
        r++; rows++;
      }

      // Axis (facoltativo)
      if (axis != null && axis.Any())
      {
        var wsA = wb.AddWorksheet("ProgesiAxisVariable");
        WriteHeader(wsA, new[] { "Id", "Hash", "Name", "Value", "Unit", "By", "LM", "Info" });
        r = 2;
        foreach (var a in axis)
        {
          wsA.Cell(r, 1).SetValue(GetInt(a, "Id"));
          wsA.Cell(r, 2).SetValue(GetStr(a, "Hash"));
          wsA.Cell(r, 3).SetValue(GetStr(a, "Name"));
          wsA.Cell(r, 4).SetValue(GetStr(a, "Value"));
          wsA.Cell(r, 5).SetValue(GetStr(a, "Unit"));
          wsA.Cell(r, 6).SetValue(GetStr(a, "By"));
          wsA.Cell(r, 7).SetValue(GetStr(a, "LM", "LastModifiedUtc") ?? NowIso());
          wsA.Cell(r, 8).SetValue(GetStr(a, "Info"));
          r++; rows++;
        }
      }

      wb.SaveAs(xlsxPath);
      return rows; // totale righe scritte (vars + metas [+ axis])
    }

    // ---------- small helpers ----------
    private static IEnumerable<object> Enumerate(object store, params string[] propNames)
    {
      if (store == null) yield break;
      foreach (var pn in propNames ?? Array.Empty<string>())
      {
        var p = store.GetType().GetProperty(pn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p == null) continue;
        var enumerable = p.GetValue(store) as System.Collections.IEnumerable;
        if (enumerable == null) continue;
        foreach (var it in enumerable) yield return it;
        yield break;
      }
    }

    private static void WriteHeader(IXLWorksheet ws, string[] cols)
    {
      for (int i = 0; i < cols.Length; i++)
        ws.Cell(1, i + 1).SetValue(cols[i]).Style.Font.SetBold(true);
      ws.Columns().AdjustToContents();
    }

    private static int GetInt(object o, string name)
    {
      var v = GetValue(o, name);
      if (v == null) return 0;
      try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return 0; }
    }
    private static string GetStr(object o, params string[] names)
    {
      foreach (var n in names)
      {
        var v = GetValue(o, n);
        if (v != null)
        {
          var s = v.ToString();
          if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        }
      }
      return null;
    }
    private static object GetValue(object o, string name)
    {
      if (o == null || string.IsNullOrEmpty(name)) return null;
      var p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (p != null) return p.GetValue(o);
      var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (f != null) return f.GetValue(o);
      return null;
    }
    private static string NowIso() => DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
    private static string FirstOf(string refsStr)
    {
      if (string.IsNullOrWhiteSpace(refsStr)) return null;
      var i = refsStr.IndexOf(';');
      return i > 0 ? refsStr.Substring(0, i).Trim() : refsStr.Trim();
    }
  }
}
