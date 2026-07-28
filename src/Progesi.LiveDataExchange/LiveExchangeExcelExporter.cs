using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Progesi.GhExcelReadContract;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeExcelExporter
  {
    public static (string path, string info) Export(LiveExchangeSnapshot snapshot, string inPath, bool overwrite)
    {
      if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

      string p = LiveExchangePathNormalizer.NormalizeExcelExportPath(inPath);
      if (File.Exists(p))
      {
        if (!overwrite) throw new InvalidOperationException("File already exists: " + p);
        try { using (var _ = File.Open(p, FileMode.Open, FileAccess.Write, FileShare.None)) { } } catch { }
      }

      var vars = snapshot.Variables ?? Array.Empty<VariableExportRow>();
      var metas = snapshot.Metadata ?? Array.Empty<MetadataExportRow>();
      var clusters = snapshot.Clusters ?? Array.Empty<ClusterExportRow>();
      int unsupportedVarExports = vars.Count(v => v.IsExcelUnsupported);

      var objectChunkRows = new List<GhExcelObjectSheet.ObjectChunkRow>();
      foreach (var v in vars)
      {
        if (string.IsNullOrWhiteSpace(v.ObjectPayloadJson))
          continue;
        objectChunkRows.AddRange(GhExcelObjectSheet.ChunkPayload(v.Id, v.ObjectType, v.ObjectPayloadJson));
      }

      using (var wb = new XLWorkbook())
      {
        WriteVariables(wb, vars);
        WriteMetadata(wb, metas);
        WriteClusters(wb, clusters);
        if (objectChunkRows.Count > 0)
          WriteObjectChunks(wb, objectChunkRows);
        wb.SaveAs(p);
      }

      string info = $"OK ExportExcel → {p} (Vars:{vars.Length}, Meta:{metas.Length}, Clusters:{clusters.Length}, ObjectChunks:{objectChunkRows.Count})";
      if (unsupportedVarExports > 0)
        info += $" | UnsupportedVarValues:{unsupportedVarExports}";
      return (p, info);
    }

    private static void WriteVariables(XLWorkbook wb, VariableExportRow[] vars)
    {
      var wsV = wb.Worksheets.Add(GhExcelSheetNames.Variables);
      wsV.Cell(1, 1).Value = "Id";
      wsV.Cell(1, 2).Value = "Hash";
      wsV.Cell(1, 3).Value = "Name";
      wsV.Cell(1, 4).Value = "Value";
      wsV.Cell(1, 5).Value = "ValC";
      wsV.Cell(1, 6).Value = "MetaId";
      wsV.Cell(1, 7).Value = "Depends";
      wsV.Cell(1, 8).Value = "Assumption";
      int r = 2;
      foreach (var v in vars)
      {
        wsV.Cell(r, 1).Value = v.Id;
        wsV.Cell(r, 2).Value = v.Hash ?? "";
        wsV.Cell(r, 3).Value = v.Name ?? "";
        wsV.Cell(r, 4).Value = v.Value ?? "";
        wsV.Cell(r, 5).Value = v.ValC ?? "";
        wsV.Cell(r, 6).Value = LiveExchangeMetadataIds.FormatForExcel(v.MetadataIds);
        wsV.Cell(r, 7).Value = (v.Depends != null && v.Depends.Length > 0) ? string.Join(",", v.Depends) : "";
        wsV.Cell(r, 8).Value = v.Assumption ? 1 : 0;
        r++;
      }
      wsV.Columns().AdjustToContents();
    }

    private static void WriteMetadata(XLWorkbook wb, MetadataExportRow[] metas)
    {
      var wsM = wb.Worksheets.Add(GhExcelSheetNames.Metadata);
      wsM.Cell(1, 1).Value = "Id";
      wsM.Cell(1, 2).Value = "Hash";
      wsM.Cell(1, 3).Value = "By";
      wsM.Cell(1, 4).Value = "Description";
      wsM.Cell(1, 5).Value = "Refs";
      wsM.Cell(1, 6).Value = "LM";
      int r2 = 2;
      foreach (var m in metas)
      {
        wsM.Cell(r2, 1).Value = m.Id;
        wsM.Cell(r2, 2).Value = m.Hash ?? "";
        wsM.Cell(r2, 3).Value = m.By ?? "";
        wsM.Cell(r2, 4).Value = m.Description ?? "";
        wsM.Cell(r2, 5).Value = (m.Refs != null && m.Refs.Length > 0) ? string.Join("|", m.Refs) : "";
        wsM.Cell(r2, 6).Value = m.LM ?? "";
        r2++;
      }
      wsM.Columns().AdjustToContents();
    }

    private static void WriteClusters(XLWorkbook wb, ClusterExportRow[] clusters)
    {
      var wsC = wb.Worksheets.Add(GhExcelSheetNames.Clusters);
      wsC.Cell(1, 1).Value = "Id";
      wsC.Cell(1, 2).Value = "Hash";
      wsC.Cell(1, 3).Value = "Name";
      wsC.Cell(1, 4).Value = "Description";
      wsC.Cell(1, 5).Value = "VariableIds";
      int r3 = 2;
      foreach (var c in clusters)
      {
        wsC.Cell(r3, 1).Value = c.Id;
        wsC.Cell(r3, 2).Value = c.Hash ?? "";
        wsC.Cell(r3, 3).Value = c.Name ?? "";
        wsC.Cell(r3, 4).Value = c.Description ?? "";
        wsC.Cell(r3, 5).Value = (c.VariableIds != null && c.VariableIds.Length > 0)
          ? string.Join(",", c.VariableIds)
          : "";
        r3++;
      }
      wsC.Columns().AdjustToContents();
    }

    private static void WriteObjectChunks(XLWorkbook wb, List<GhExcelObjectSheet.ObjectChunkRow> objectChunkRows)
    {
      var wsO = wb.Worksheets.Add(GhExcelSheetNames.VariableObjects);
      wsO.Cell(1, 1).Value = "VarId";
      wsO.Cell(1, 2).Value = "ChunkIndex";
      wsO.Cell(1, 3).Value = "ChunkCount";
      wsO.Cell(1, 4).Value = "ObjectType";
      wsO.Cell(1, 5).Value = "Payload";
      int r4 = 2;
      foreach (var chunk in objectChunkRows)
      {
        wsO.Cell(r4, 1).Value = chunk.VarId;
        wsO.Cell(r4, 2).Value = chunk.ChunkIndex;
        wsO.Cell(r4, 3).Value = chunk.ChunkCount;
        wsO.Cell(r4, 4).Value = chunk.ObjectType ?? "";
        wsO.Cell(r4, 5).Value = chunk.Payload ?? "";
        r4++;
      }
      wsO.Columns().AdjustToContents();
    }
  }
}
