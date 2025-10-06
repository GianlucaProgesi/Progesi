using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Progesi.Grasshopper.Browsers.Internal
{
  internal sealed class SqliteBrowser : IDisposable
  {
    private readonly SqliteConnection _cn;
    private bool _disposed;

    public SqliteBrowser(string dbPath)
    {
      if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("DbPath is empty.");
      var full = Path.GetFullPath(dbPath);
      if (!File.Exists(full)) throw new FileNotFoundException($"Database not found: {full}", full);
      _cn = new SqliteConnection($"Data Source={full};Mode=ReadOnly;");
      _cn.Open();
    }

    public void Dispose()
    {
      if (_disposed) return;
      _cn.Dispose();
      _disposed = true;
    }

    public HashSet<string> GetColumns(string table)
    {
      var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      using (var cmd = _cn.CreateCommand())
      {
        cmd.CommandText = $"PRAGMA table_info({table});";
        using (var rd = cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            var name = rd["name"]?.ToString();
            if (!string.IsNullOrEmpty(name)) cols.Add(name);
          }
        }
      }
      return cols;
    }

    public (List<string> Headers, List<string[]> Rows) BrowseVariables(string filterHash, string filterName, string filterBy, int limit)
    {
      var cols = GetColumns("variables");
      var headers = new List<string> { "#", "Id", "Hash" };
      var hasName = cols.Contains("Name");
      var hasValue = cols.Contains("Value");
      var hasUnit = cols.Contains("Unit");
      var hasRef = cols.Contains("Ref");
      var hasLast = cols.Contains("LastModifiedUtc");
      var byCol = ResolveByColumn(cols);

      if (hasName) headers.Add("Name");
      if (hasValue) headers.Add("Value");
      if (hasUnit) headers.Add("Unit");
      if (byCol != null) headers.Add(byCol);
      if (hasRef) headers.Add("Ref");
      if (hasLast) headers.Add("LastModifiedUtc");

      var selectCols = new List<string> { "Id", "Hash" };
      if (hasName) selectCols.Add("Name");
      if (hasValue) selectCols.Add("Value");
      if (hasUnit) selectCols.Add("Unit");
      if (byCol != null) selectCols.Add(byCol);
      if (hasRef) selectCols.Add("Ref");
      if (hasLast) selectCols.Add("LastModifiedUtc");

      var where = new List<string>();
      var prm = new List<(string, object)>();
      if (!string.IsNullOrWhiteSpace(filterHash)) { where.Add("Hash LIKE @hash"); prm.Add(("@hash", $"%{filterHash}%")); }
      if (hasName && !string.IsNullOrWhiteSpace(filterName)) { where.Add("Name LIKE @name"); prm.Add(("@name", $"%{filterName}%")); }
      if (byCol != null && !string.IsNullOrWhiteSpace(filterBy)) { where.Add($"{byCol} LIKE @by"); prm.Add(("@by", $"%{filterBy}%")); }

      var sql = $"SELECT {string.Join(", ", selectCols)} FROM variables";
      if (where.Count > 0) sql += " WHERE " + string.Join(" AND ", where);
      if (hasLast) sql += " ORDER BY LastModifiedUtc DESC";
      sql += " LIMIT @limit";

      var rows = new List<string[]>();
      using (var cmd = _cn.CreateCommand())
      {
        cmd.CommandText = sql;
        foreach (var (name, val) in prm)
          cmd.Parameters.Add(new SqliteParameter(name, val));
        cmd.Parameters.Add(new SqliteParameter("@limit", Math.Max(1, Math.Min(1000, limit))));
        using (var rd = cmd.ExecuteReader())
        {
          int i = 0;
          while (rd.Read())
          {
            var list = new List<string> { (++i).ToString(), SafeStr(rd, "Id"), SafeStr(rd, "Hash") };
            if (hasName) list.Add(SafeStr(rd, "Name"));
            if (hasValue) list.Add(SafeStr(rd, "Value"));
            if (hasUnit) list.Add(SafeStr(rd, "Unit"));
            if (byCol != null) list.Add(SafeStr(rd, byCol));
            if (hasRef) list.Add(SafeStr(rd, "Ref"));
            if (hasLast) list.Add(SafeStr(rd, "LastModifiedUtc"));
            rows.Add(list.ToArray());
          }
        }
      }
      return (headers, rows);
    }

    public (List<string> Headers, List<string[]> Rows) BrowseMetadata(string filterHash, string filterBy, string filterRef, int limit)
    {
      var cols = GetColumns("metadata");
      var headers = new List<string> { "#", "Id", "Hash" };
      var hasInfo = cols.Contains("Info");
      var hasRef = cols.Contains("Ref");
      var hasLast = cols.Contains("LastModifiedUtc");
      var byCol = ResolveByColumn(cols);

      if (hasInfo) headers.Add("Info");
      if (byCol != null) headers.Add(byCol);
      if (hasRef) headers.Add("Ref");
      if (hasLast) headers.Add("LastModifiedUtc");

      var selectCols = new List<string> { "Id", "Hash" };
      if (hasInfo) selectCols.Add("Info");
      if (byCol != null) selectCols.Add(byCol);
      if (hasRef) selectCols.Add("Ref");
      if (hasLast) selectCols.Add("LastModifiedUtc");

      var where = new List<string>();
      var prm = new List<(string, object)>();
      if (!string.IsNullOrWhiteSpace(filterHash)) { where.Add("Hash LIKE @hash"); prm.Add(("@hash", $"%{filterHash}%")); }
      if (byCol != null && !string.IsNullOrWhiteSpace(filterBy)) { where.Add($"{byCol} LIKE @by"); prm.Add(("@by", $"%{filterBy}%")); }
      if (hasRef && !string.IsNullOrWhiteSpace(filterRef)) { where.Add("Ref LIKE @ref"); prm.Add(("@ref", $"%{filterRef}%")); }

      var sql = $"SELECT {string.Join(", ", selectCols)} FROM metadata";
      if (where.Count > 0) sql += " WHERE " + string.Join(" AND ", where);
      if (hasLast) sql += " ORDER BY LastModifiedUtc DESC";
      sql += " LIMIT @limit";

      var rows = new List<string[]>();
      using (var cmd = _cn.CreateCommand())
      {
        cmd.CommandText = sql;
        foreach (var (name, val) in prm) cmd.Parameters.Add(new SqliteParameter(name, val));
        cmd.Parameters.Add(new SqliteParameter("@limit", Math.Max(1, Math.Min(1000, limit))));
        using (var rd = cmd.ExecuteReader())
        {
          int i = 0;
          while (rd.Read())
          {
            var list = new List<string> { (++i).ToString(), SafeStr(rd, "Id"), SafeStr(rd, "Hash") };
            if (hasInfo) list.Add(SafeStr(rd, "Info"));
            if (byCol != null) list.Add(SafeStr(rd, byCol));
            if (hasRef) list.Add(SafeStr(rd, "Ref"));
            if (hasLast) list.Add(SafeStr(rd, "LastModifiedUtc"));
            rows.Add(list.ToArray());
          }
        }
      }
      return (headers, rows);
    }

    private static string SafeStr(IDataRecord rd, string name)
    {
      try
      {
        var ordinal = rd.GetOrdinal(name);
        if (rd.IsDBNull(ordinal)) return "";
        return Convert.ToString(rd.GetValue(ordinal)) ?? "";
      }
      catch { return ""; }
    }

    private static string ResolveByColumn(HashSet<string> cols)
    {
      foreach (var c in new[] { "By", "CreatedBy", "Author", "ModifiedBy" })
        if (cols.Contains(c)) return c;
      return null;
    }
  }
}
