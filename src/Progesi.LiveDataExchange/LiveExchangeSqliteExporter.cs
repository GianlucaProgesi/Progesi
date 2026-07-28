using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeSqliteExporter
  {
    public static (string path, string info) Export(LiveExchangeSnapshot snapshot, string inPath, bool overwrite)
    {
      if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

      var metas = snapshot.Metadata ?? Array.Empty<MetadataExportRow>();
      var vars = snapshot.Variables ?? Array.Empty<VariableExportRow>();
      var clusters = snapshot.Clusters ?? Array.Empty<ClusterExportRow>();
      var metaIdsPresent = new HashSet<int>(metas.Select(m => m.Id));

      string p = LiveExchangePathNormalizer.NormalizeSqliteExportPath(inPath);
      p = LiveExchangePathNormalizer.PrepareSqliteExportPath(p, overwrite);

      using (var cn = new SQLiteConnection($@"Data Source={p};Version=3;"))
      {
        cn.Open();

        using (var tx = cn.BeginTransaction())
        using (var cmd = new SQLiteCommand(cn))
        {
          cmd.CommandText = "PRAGMA foreign_keys=ON;";
          cmd.ExecuteNonQuery();

          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  By           TEXT,
  Description  TEXT,
  LM           TEXT
);
CREATE TABLE IF NOT EXISTS Variables (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  Name         TEXT NOT NULL,
  Value        TEXT,
  ValC         TEXT,
  MetaId       INTEGER NULL,
  Assumption   INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS Refs (
  MetaId       INTEGER NOT NULL,
  Ref          TEXT NOT NULL,
  PRIMARY KEY (MetaId, Ref),
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS VariableDepends (
  VarId        INTEGER NOT NULL,
  DepId        INTEGER NOT NULL,
  PRIMARY KEY (VarId, DepId),
  FOREIGN KEY (VarId) REFERENCES Variables(Id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS Clusters (
  Id          INTEGER PRIMARY KEY,
  Hash        TEXT NOT NULL,
  Name        TEXT NOT NULL,
  Description TEXT
);

CREATE TABLE IF NOT EXISTS ClusterVariables (
  ClusterId   INTEGER NOT NULL,
  VarId       INTEGER NOT NULL,
  PRIMARY KEY (ClusterId, VarId),
  FOREIGN KEY (ClusterId) REFERENCES Clusters(Id) ON DELETE CASCADE,
  FOREIGN KEY (VarId)     REFERENCES Variables(Id) ON DELETE CASCADE
);";
          cmd.ExecuteNonQuery();

          cmd.CommandText = "INSERT OR REPLACE INTO Metadata (Id,Hash,By,Description,LM) VALUES (@id,@hash,@by,@descr,@lm)";
          var pId = new SQLiteParameter("@id");
          var pHash = new SQLiteParameter("@hash");
          var pBy = new SQLiteParameter("@by");
          var pDescr = new SQLiteParameter("@descr");
          var pLM = new SQLiteParameter("@lm");
          cmd.Parameters.AddRange(new[] { pId, pHash, pBy, pDescr, pLM });

          foreach (var m in metas)
          {
            pId.Value = m.Id;
            pHash.Value = m.Hash ?? string.Empty;
            pBy.Value = m.By ?? string.Empty;
            pDescr.Value = m.Description ?? string.Empty;
            pLM.Value = m.LM ?? string.Empty;
            cmd.ExecuteNonQuery();
          }

          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO Variables (Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (@id,@hash,@name,@value,@valc,@mid,@ass)";
          var vId = new SQLiteParameter("@id");
          var vHash = new SQLiteParameter("@hash");
          var vName = new SQLiteParameter("@name");
          var vVal = new SQLiteParameter("@value");
          var vValC = new SQLiteParameter("@valc");
          var vMid = new SQLiteParameter("@mid");
          var vAss = new SQLiteParameter("@ass");
          cmd.Parameters.AddRange(new[] { vId, vHash, vName, vVal, vValC, vMid, vAss });

          foreach (var v in vars)
          {
            vId.Value = v.Id;
            vHash.Value = v.Hash ?? string.Empty;
            vName.Value = v.Name ?? string.Empty;
            vVal.Value = v.Value ?? string.Empty;
            vValC.Value = v.ValC ?? string.Empty;
            vMid.Value = (v.MetaId > 0 && metaIdsPresent.Contains(v.MetaId)) ? (object)v.MetaId : DBNull.Value;
            vAss.Value = v.Assumption ? 1 : 0;
            cmd.ExecuteNonQuery();
          }

          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO Refs (MetaId,Ref) VALUES (@mid,@ref)";
          var rMid = new SQLiteParameter("@mid");
          var rRef = new SQLiteParameter("@ref");
          cmd.Parameters.AddRange(new[] { rMid, rRef });

          foreach (var m in metas)
          {
            if (m.Refs == null || m.Refs.Length == 0) continue;
            if (!metaIdsPresent.Contains(m.Id)) continue;

            foreach (var rf in m.Refs)
            {
              rMid.Value = m.Id;
              rRef.Value = rf ?? string.Empty;
              cmd.ExecuteNonQuery();
            }
          }

          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO VariableDepends (VarId,DepId) VALUES (@vid,@did)";
          var dVid = new SQLiteParameter("@vid");
          var dDid = new SQLiteParameter("@did");
          cmd.Parameters.AddRange(new[] { dVid, dDid });

          var varIds = new HashSet<int>(vars.Select(v => v.Id));
          foreach (var v in vars)
          {
            if (v.Depends == null || v.Depends.Length == 0) continue;
            foreach (var dep in v.Depends)
            {
              if (!varIds.Contains(dep)) continue;
              dVid.Value = v.Id;
              dDid.Value = dep;
              cmd.ExecuteNonQuery();
            }
          }

          cmd.Parameters.Clear();
          cmd.CommandText = "DELETE FROM ClusterVariables;";
          cmd.ExecuteNonQuery();
          cmd.CommandText = "DELETE FROM Clusters;";
          cmd.ExecuteNonQuery();

          cmd.CommandText = "INSERT OR REPLACE INTO Clusters (Id,Hash,Name,Description) VALUES (@id,@hash,@name,@desc)";
          var cId = new SQLiteParameter("@id");
          var cHash = new SQLiteParameter("@hash");
          var cName = new SQLiteParameter("@name");
          var cDesc = new SQLiteParameter("@desc");
          cmd.Parameters.AddRange(new[] { cId, cHash, cName, cDesc });

          foreach (var c in clusters)
          {
            cId.Value = c.Id;
            cHash.Value = c.Hash ?? string.Empty;
            cName.Value = c.Name ?? string.Empty;
            cDesc.Value = c.Description ?? string.Empty;
            cmd.ExecuteNonQuery();
          }

          cmd.Parameters.Clear();
          cmd.CommandText = "INSERT OR REPLACE INTO ClusterVariables (ClusterId,VarId) VALUES (@cid,@vid)";
          var jCid = new SQLiteParameter("@cid");
          var jVid = new SQLiteParameter("@vid");
          cmd.Parameters.AddRange(new[] { jCid, jVid });

          foreach (var c in clusters)
          {
            if (c.VariableIds == null || c.VariableIds.Length == 0) continue;

            foreach (var vid in c.VariableIds)
            {
              if (!varIds.Contains(vid)) continue;

              jCid.Value = c.Id;
              jVid.Value = vid;
              cmd.ExecuteNonQuery();
            }
          }

          tx.Commit();
        }
      }

      var info = string.Format(
        CultureInfo.InvariantCulture,
        "OK ExportSqlite → {0} (Meta:{1}, Vars:{2}, Clusters:{3})",
        p, metas.Length, vars.Length, clusters.Length);
      return (p, info);
    }
  }
}
