using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using Progesi.GhExcelReadContract;

namespace Progesi.GhExcelReadContract.Tests.Support
{
  internal static class CanonicalExchangeWorkbookBuilder
  {
    internal sealed class HeaderStyle
    {
      public bool UseAliasHeaders { get; set; }

      public string[] VariableHeaders => UseAliasHeaders
        ? new[] { "VarId", "Digest", "Field", "Val", "ValueCanonical", "Mid", "Deps", "Ass" }
        : new[] { "Id", "Hash", "Name", "Value", "ValC", "MetaId", "Depends", "Assumption" };

      public string[] MetadataHeaders => UseAliasHeaders
        ? new[] { "MetaId", "Digest", "Author", "Desc", "Links", "LastModified" }
        : new[] { "Id", "Hash", "By", "Description", "Refs", "LM" };

      public string[] ClusterHeaders => UseAliasHeaders
        ? new[] { "ClusterId", "Hashtag", "ClusterName", "Info", "Members" }
        : new[] { "Id", "Hash", "Name", "Description", "VariableIds" };

      public string[] ObjectHeaders => new[] { "VarId", "ChunkIndex", "ChunkCount", "ObjectType", "Payload" };
    }

    internal static void Write(string path, CanonicalExchangeModel model, HeaderStyle style = null)
    {
      if (model == null) throw new ArgumentNullException(nameof(model));
      style = style ?? new HeaderStyle();

      using var wb = new XLWorkbook();
      WriteVariablesSheet(wb, model.Variables, style);
      WriteMetadataSheet(wb, model.Metadata, style);
      WriteClustersSheet(wb, model.Clusters, style);
      WriteObjectChunksSheet(wb, model.Variables);
      wb.SaveAs(path);
    }

    private static void WriteVariablesSheet(XLWorkbook wb, IReadOnlyList<VariableRow> variables, HeaderStyle style)
    {
      var ws = wb.Worksheets.Add(GhExcelSheetNames.Variables);
      var headers = style.VariableHeaders;
      for (int c = 0; c < headers.Length; c++)
        ws.Cell(1, c + 1).Value = headers[c];

      int row = 2;
      foreach (var v in variables)
      {
        ws.Cell(row, 1).Value = v.Id;
        ws.Cell(row, 2).Value = v.Hash ?? "";
        ws.Cell(row, 3).Value = v.Name ?? "";
        ws.Cell(row, 4).Value = v.Value ?? "";
        ws.Cell(row, 5).Value = v.ValC ?? "";
        ws.Cell(row, 6).Value = FormatMetaIdForExcel(v.MetaIds);
        ws.Cell(row, 7).Value = v.Depends != null && v.Depends.Length > 0
          ? string.Join(",", v.Depends)
          : "";
        ws.Cell(row, 8).Value = v.Assumption ? 1 : 0;
        row++;
      }
    }

    private static void WriteMetadataSheet(XLWorkbook wb, IReadOnlyList<MetadataRow> metadata, HeaderStyle style)
    {
      var ws = wb.Worksheets.Add(GhExcelSheetNames.Metadata);
      var headers = style.MetadataHeaders;
      for (int c = 0; c < headers.Length; c++)
        ws.Cell(1, c + 1).Value = headers[c];

      int row = 2;
      foreach (var m in metadata)
      {
        ws.Cell(row, 1).Value = m.Id;
        ws.Cell(row, 2).Value = m.Hash ?? "";
        ws.Cell(row, 3).Value = m.By ?? "";
        ws.Cell(row, 4).Value = m.Description ?? "";
        ws.Cell(row, 5).Value = m.Refs != null && m.Refs.Length > 0
          ? string.Join("|", m.Refs)
          : "";
        ws.Cell(row, 6).Value = m.LastModified ?? "";
        row++;
      }
    }

    private static void WriteClustersSheet(XLWorkbook wb, IReadOnlyList<ClusterRow> clusters, HeaderStyle style)
    {
      var ws = wb.Worksheets.Add(GhExcelSheetNames.Clusters);
      var headers = style.ClusterHeaders;
      for (int c = 0; c < headers.Length; c++)
        ws.Cell(1, c + 1).Value = headers[c];

      int row = 2;
      foreach (var c in clusters)
      {
        ws.Cell(row, 1).Value = c.Id;
        ws.Cell(row, 2).Value = c.Hash ?? "";
        ws.Cell(row, 3).Value = c.Name ?? "";
        ws.Cell(row, 4).Value = c.Description ?? "";
        ws.Cell(row, 5).Value = c.VariableIds != null && c.VariableIds.Length > 0
          ? string.Join(",", c.VariableIds)
          : "";
        row++;
      }
    }

    private static void WriteObjectChunksSheet(XLWorkbook wb, IReadOnlyList<VariableRow> variables)
    {
      var chunks = new List<GhExcelObjectSheet.ObjectChunkRow>();
      foreach (var v in variables)
      {
        if (string.IsNullOrWhiteSpace(v.ObjectPayloadJson) || string.IsNullOrWhiteSpace(v.ObjectType))
          continue;
        chunks.AddRange(GhExcelObjectSheet.ChunkPayload(v.Id, v.ObjectType, v.ObjectPayloadJson));
      }

      if (chunks.Count == 0)
        return;

      var ws = wb.Worksheets.Add(GhExcelSheetNames.VariableObjects);
      var headers = new HeaderStyle().ObjectHeaders;
      for (int c = 0; c < headers.Length; c++)
        ws.Cell(1, c + 1).Value = headers[c];

      int row = 2;
      foreach (var chunk in chunks)
      {
        ws.Cell(row, 1).Value = chunk.VarId;
        ws.Cell(row, 2).Value = chunk.ChunkIndex;
        ws.Cell(row, 3).Value = chunk.ChunkCount;
        ws.Cell(row, 4).Value = chunk.ObjectType ?? "";
        ws.Cell(row, 5).Value = chunk.Payload ?? "";
        row++;
      }
    }

    internal static string FormatMetaIdForExcel(int[] metadataIds)
    {
      if (metadataIds == null || metadataIds.Length == 0)
        return string.Empty;

      if (metadataIds.Length == 1)
        return metadataIds[0].ToString(CultureInfo.InvariantCulture);

      return string.Join(",", metadataIds);
    }
  }
}
