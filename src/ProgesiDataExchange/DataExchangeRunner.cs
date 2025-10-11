using System;
using System.IO;

namespace Progesi.DataExchange
{
  public static class DataExchangeRunner
  {
    public static DataExchangeReport Run(
      DataExchangeAction action,
      IProgesiStore rhinoStore,
      string sqlitePath,
      string excelPath,
      bool createDbIfMissing)
    {
      var report = new DataExchangeReport();
      bool hasSql = !string.IsNullOrWhiteSpace(sqlitePath);
      bool hasXls = !string.IsNullOrWhiteSpace(excelPath);

      if (!hasSql && !hasXls) { report.Lines.Add("No source/destination (DbPath/ExcelPath both empty)."); return report; }

      if (action == DataExchangeAction.Read)
      {
        // 1) SQL -> Rhino
        if (hasSql)
        {
          try
          {
            using var sql = new SqliteStore(sqlitePath, createIfMissing: false);
            var (vi, vu, vs, nh1) = MergeService.MergeVariables(sql.GetAllVariables(), rhinoStore);
            var (mi, mu, ms, nh2) = MergeService.MergeMetadata(sql.GetAllMetadata(), rhinoStore);
            var (ai, au, as_, nh3) = MergeService.MergeAxis(sql.GetAllAxisVariables(), rhinoStore);
            report.ReadSql = (vi + mi + ai, vu + mu + au, vs + ms + as_);
            report.NewHashes.AddRange(nh1); report.NewHashes.AddRange(nh2); report.NewHashes.AddRange(nh3);
            report.Lines.Add($"READ SQL: Var ins={vi} upd={vu} skip={vs} | Met ins={mi} upd={mu} skip={ms} | Axis ins={ai} upd={au} skip={as_}");
          }
          catch (FileNotFoundException) { report.Lines.Add($"READ SQL: skipped (db not found: {sqlitePath})"); }
        }

        // 2) XLSX -> Rhino
        if (hasXls)
        {
          if (File.Exists(excelPath))
          {
            var (vars, mets, axis) = ExcelSerializer.Read(excelPath);
            var (vi, vu, vs, nh1) = MergeService.MergeVariables(vars, rhinoStore);
            var (mi, mu, ms, nh2) = MergeService.MergeMetadata(mets, rhinoStore);
            var (ai, au, as_, nh3) = MergeService.MergeAxis(axis, rhinoStore);
            report.ReadXlsx = (vi + mi + ai, vu + mu + au, vs + ms + as_);
            report.NewHashes.AddRange(nh1); report.NewHashes.AddRange(nh2); report.NewHashes.AddRange(nh3);
            report.Lines.Add($"READ XLSX: Var ins={vi} upd={vu} skip={vs} | Met ins={mi} upd={mu} skip={ms} | Axis ins={ai} upd={au} skip={as_}");
          }
          else report.Lines.Add($"READ XLSX: skipped (file not found: {excelPath})");
        }
      }
      else // WRITE
      {
        // 1) Rhino -> SQL
        if (hasSql)
        {
          using var sql = new SqliteStore(sqlitePath, createIfMissing: createDbIfMissing);
          var vi = sql.UpsertVariables(rhinoStore.GetAllVariables());
          var mi = sql.UpsertMetadata(rhinoStore.GetAllMetadata());
          var ai = sql.UpsertAxisVariables(rhinoStore.GetAllAxisVariables());
          report.WriteSql = (vi.inserted + mi.inserted + ai.inserted, vi.updated + mi.updated + ai.updated, vi.skipped + mi.skipped + ai.skipped);
          report.Lines.Add($"WRITE SQL: ins={report.WriteSql.ins} upd={report.WriteSql.upd} skip={report.WriteSql.skip} -> {sqlitePath}");
        }

        // 2) Rhino -> XLSX
        if (hasXls)
        {
          var vars = rhinoStore.GetAllVariables();
          var mets = rhinoStore.GetAllMetadata();
          var axis = rhinoStore.GetAllAxisVariables();
          ExcelSerializer.Write(excelPath, vars, mets, axis);
          report.WriteXlsx = (vars.Count + mets.Count + axis.Count, 0, 0);
          report.Lines.Add($"WRITE XLSX: rows={report.WriteXlsx.ins} -> {excelPath}");
        }
      }
      return report;
    }
  }
}
