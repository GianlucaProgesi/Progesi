using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ProgesiCore;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeSqliteImporter
  {
    public static LiveExchangeImportResult ImportValidated(
      string inDbPath,
      bool strict,
      bool dryRun,
      ILiveExchangeImportSink sink)
    {
      if (sink == null) throw new ArgumentNullException(nameof(sink));

      string db = (inDbPath ?? "").Trim();
      if (string.IsNullOrEmpty(db)) throw new ArgumentException("SQLite path not specified.");
      if (!File.Exists(db)) throw new FileNotFoundException("SQLite file not found.", db);

      var result = new LiveExchangeImportResult { SourcePath = db };
      void LOG(string lvl, string msg) => result.LogLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {lvl}: {msg}");
      void WARN(int br, string msg) { result.Warnings.Add(new LiveExchangeMessage { Branch = br, Text = msg }); LOG("WARN", msg); }
      void ERR(int br, string msg) { result.Errors.Add(new LiveExchangeMessage { Branch = br, Text = msg }); LOG("ERROR", msg); }
      void AddErrRC(int br, int row, int col) => result.ErrorCoordinates.Add(new LiveExchangeErrorCoordinate { Branch = br, Row = row, Col = col });
      Action<int, string> Report = strict ? (Action<int, string>)ERR : WARN;

      const int NAME_MAX = 128;
      const int DESC_MAX = 512;
      Func<string, bool> IsPrintable = s => string.IsNullOrEmpty(s) || s.All(ch => ch >= 32 && ch != 127);
      Func<string, bool> IsHttpUrl = s => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");
      Func<string, bool> IsAbsPath = s => !string.IsNullOrEmpty(s) && Path.IsPathRooted(s);

      int metaRows = 0, metaOk = 0, metaWarn = 0, metaErr = 0;
      int varRows = 0, varOk = 0, varWarn = 0, varErr = 0;
      int clusterRows = 0, clusterOk = 0, clusterWarn = 0, clusterErr = 0;

      using (var cn = new SQLiteConnection($@"Data Source={db};Version=3;Read Only=True;"))
      {
        cn.Open();

        bool HasTable(string name)
        {
          using (var chk = new SQLiteCommand(cn))
          {
            chk.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@n";
            chk.Parameters.AddWithValue("@n", name);
            var r = chk.ExecuteScalar();
            return r != null && r != DBNull.Value;
          }
        }

        if (!HasTable("Metadata") || !HasTable("Variables"))
        {
          ERR(0, "SQLite schema not found (Metadata/Variables). Did export fail previously?");
          string bad = db + ".import.log.txt";
          try { File.WriteAllLines(bad, result.LogLines, Encoding.UTF8); } catch { bad = ""; }
          result.LogPath = bad;
          result.Info = "Error: missing tables in SQLite database.";
          return result;
        }

        using (var cmd = new SQLiteCommand(cn))
        {
          cmd.CommandText = "SELECT Id, By, Description, LM FROM Metadata ORDER BY Id";
          using (var rd = cmd.ExecuteReader())
          {
            int row = 0;
            while (rd.Read())
            {
              row++; metaRows++;
              int id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
              string by = rd.IsDBNull(1) ? "" : rd.GetString(1);
              string ds = rd.IsDBNull(2) ? "" : rd.GetString(2);

              if (by.Length > NAME_MAX || !IsPrintable(by))
              { var msg = $"[Meta R{row}] BY invalid (len/charset)"; Report(0, msg); AddErrRC(0, row, 2); if (strict) { metaErr++; continue; } else { metaWarn++; } }

              if (ds.Length > DESC_MAX || !IsPrintable(ds))
              { var msg = $"[Meta R{row}] DESCRIPTION invalid (len/charset)"; Report(0, msg); AddErrRC(0, row, 3); if (strict) { metaErr++; continue; } else { metaWarn++; } }

              string refsJoined = "";
              using (var cmdR = new SQLiteCommand(cn))
              {
                cmdR.CommandText = "SELECT Ref FROM Refs WHERE MetaId=@id";
                cmdR.Parameters.AddWithValue("@id", id);
                using (var rr = cmdR.ExecuteReader())
                {
                  var refs = new List<string>();
                  while (rr.Read())
                  {
                    string rfs = rr.IsDBNull(0) ? "" : rr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(rfs))
                    {
                      if (!(IsHttpUrl(rfs) || IsAbsPath(rfs)))
                      { var msg = $"[Meta R{row}] REF invalid: {rfs}"; Report(0, msg); AddErrRC(0, row, 5); if (strict) { metaErr++; refs.Clear(); break; } else { metaWarn++; } }
                      refs.Add(rfs);
                    }
                  }
                  refsJoined = refs.Count > 0 ? string.Join("|", refs) : "";
                }
              }

              if (!dryRun)
              {
                if (!sink.TryUpsertMetadata(id, by ?? "", ds ?? "", refsJoined, out _, out var upInfo))
                { ERR(0, $"[Meta R{row}] import failed: {upInfo ?? "unknown"}"); metaErr++; continue; }
              }

              metaOk++;
            }
          }
        }

        bool hasClusters = HasTable("Clusters") && HasTable("ClusterVariables");

        using (var cmd = new SQLiteCommand(cn))
        {
          cmd.CommandText = "SELECT Id, Name, Value, MetaId, Assumption FROM Variables ORDER BY Id";
          using (var rd = cmd.ExecuteReader())
          {
            int row = 0;
            while (rd.Read())
            {
              row++; varRows++;
              int id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
              string nm = rd.IsDBNull(1) ? "" : rd.GetString(1);
              string vl = rd.IsDBNull(2) ? "" : rd.GetString(2);
              int mid = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
              bool ass = !rd.IsDBNull(4) && (rd.GetInt32(4) != 0);

              if (nm.Length > NAME_MAX || !IsPrintable(nm))
              { var msg = $"[Var R{row}] NAME invalid (len/charset)"; Report(1, msg); AddErrRC(1, row, 2); if (strict) { varErr++; continue; } else { varWarn++; } }

              int[] dep = Array.Empty<int>();
              using (var cmdD = new SQLiteCommand(cn))
              {
                cmdD.CommandText = "SELECT DepId FROM VariableDepends WHERE VarId=@id ORDER BY DepId";
                cmdD.Parameters.AddWithValue("@id", id);
                using (var rr = cmdD.ExecuteReader())
                {
                  var tmp = new List<int>(); while (rr.Read()) tmp.Add(rr.GetInt32(0)); dep = tmp.ToArray();
                }
              }

              int[] metaIds = mid > 0 ? new[] { mid } : Array.Empty<int>();
              if (mid > 0)
              {
                if (!sink.TryGetMetadataById(mid, out _))
                { var msg = $"[Var R{row}] METAID not found: {mid}"; Report(1, msg); AddErrRC(1, row, 4); if (strict) { varErr++; continue; } else { varWarn++; metaIds = Array.Empty<int>(); } }
              }

              if (!dryRun)
              {
                var payload = new LiveExchangeVariableImportPayload
                {
                  Id = id,
                  Name = nm ?? "",
                  Value = vl ?? "",
                  GeometryJson = "",
                  IsAssumption = ass,
                  MetadataIds = metaIds,
                  Depends = dep
                };

                if (!sink.TryUpsertVariable(payload, out _, out var upInfo))
                { ERR(1, $"[Var R{row}] import failed: {upInfo ?? "unknown"}"); varErr++; continue; }
              }

              varOk++;
            }
          }
        }

        if (hasClusters && !dryRun)
        {
          using (var cmdC = new SQLiteCommand(cn))
          {
            cmdC.CommandText = "SELECT Id, Name, Description, Hash FROM Clusters ORDER BY Id";
            using (var rdC = cmdC.ExecuteReader())
            {
              while (rdC.Read())
              {
                clusterRows++;

                int cid = rdC.IsDBNull(0) ? 0 : rdC.GetInt32(0);
                string cnm = rdC.IsDBNull(1) ? "" : rdC.GetString(1);
                string cds = rdC.IsDBNull(2) ? "" : rdC.GetString(2);
                string ch = rdC.IsDBNull(3) ? "" : rdC.GetString(3);

                var varIdsList = new List<int>();
                using (var cmdJ = new SQLiteCommand(cn))
                {
                  cmdJ.CommandText = "SELECT VarId FROM ClusterVariables WHERE ClusterId=@id ORDER BY VarId";
                  cmdJ.Parameters.AddWithValue("@id", cid);
                  using (var rdJ = cmdJ.ExecuteReader())
                  {
                    while (rdJ.Read())
                    {
                      int vid = rdJ.IsDBNull(0) ? 0 : rdJ.GetInt32(0);
                      if (vid > 0) varIdsList.Add(vid);
                    }
                  }
                }

                var varIdsArr = varIdsList.Distinct().OrderBy(x => x).ToArray();
                var cluster = ProgesiVariableCluster.Rehydrate(cid, cnm ?? "", varIdsArr, cds, string.IsNullOrWhiteSpace(ch) ? null : ch);

                var clusterPayload = new LiveExchangeClusterImportPayload
                {
                  Id = cid,
                  Name = cnm ?? "",
                  Description = cds ?? "",
                  VariableIds = varIdsArr,
                  Hashtag = cluster.Hashtag
                };

                if (!sink.TryPersistCluster(clusterPayload))
                {
                  ERR(2, $"[Cluster R{clusterRows}] persist failed");
                  clusterErr++;
                  continue;
                }

                clusterOk++;
              }
            }
          }

          LOG("INFO", $"[Clusters] Imported {clusterOk}/{clusterRows} from SQLite.");
        }
      }

      string logPath = db + ".import.log.txt";
      try { File.WriteAllLines(logPath, result.LogLines, Encoding.UTF8); } catch { logPath = ""; }
      result.LogPath = logPath;

      result.Counts.Add(new LiveExchangeCountSummary { Branch = 0, Text = $"Meta rows={metaRows} ok={metaOk} warn={metaWarn} err={metaErr}" });
      result.Counts.Add(new LiveExchangeCountSummary { Branch = 1, Text = $"Vars rows={varRows} ok={varOk} warn={varWarn} err={varErr}" });
      result.Counts.Add(new LiveExchangeCountSummary { Branch = 2, Text = $"Clusters rows={clusterRows} ok={clusterOk} warn={clusterWarn} err={clusterErr}" });

      string prefix = dryRun ? "PREVIEW " : "OK ";
      result.Info = $"{prefix}ImportSqlite ← {db} | " +
                    $"Meta {metaOk}/{metaRows} (warn:{metaWarn}, err:{metaErr}) | " +
                    $"Vars {varOk}/{varRows} (warn:{varWarn}, err:{varErr}) | " +
                    $"Clusters {clusterOk}/{clusterRows} (warn:{clusterWarn}, err:{clusterErr}) | " +
                    $"Log: {(string.IsNullOrWhiteSpace(logPath) ? "-" : logPath)}";

      return result;
    }
  }
}
