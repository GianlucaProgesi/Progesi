using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using Progesi.GhExcelReadContract;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeMetadataIds
  {
    public static int[] Parse(string cellValue)
    {
      if (string.IsNullOrWhiteSpace(cellValue))
        return Array.Empty<int>();

      var trimmed = cellValue.Trim();
      if (trimmed == "0")
        return Array.Empty<int>();

      if (!trimmed.Contains(",") && !trimmed.Contains(";") && !trimmed.Contains("|"))
      {
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var single) && single > 0)
          return new[] { single };
        return Array.Empty<int>();
      }

      var tokens = trimmed.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      var seen = new HashSet<int>();
      var list = new List<int>();
      foreach (var token in tokens)
      {
        if (int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && n > 0
            && seen.Add(n))
          list.Add(n);
      }

      return list.ToArray();
    }

    public static string FormatForExcel(int[] metadataIds)
    {
      if (metadataIds == null || metadataIds.Length == 0)
        return string.Empty;

      if (metadataIds.Length == 1)
        return metadataIds[0].ToString(CultureInfo.InvariantCulture);

      return string.Join(",", metadataIds);
    }
  }

  public static class LiveExchangeObjectChunkReader
  {
    public static List<GhExcelObjectSheet.ObjectChunkRow> ReadObjectChunkRows(XLWorkbook workbook)
    {
      var rows = new List<GhExcelObjectSheet.ObjectChunkRow>();
      var wsO = GhExcelWorksheetLocator.TryGetWorksheet(
        workbook,
        GhExcelSheetNames.VariableObjects,
        GhExcelSheetNames.VariableObjectsAlias);
      if (wsO == null)
        return rows;

      var header = GhExcelHeaderMap.Build(wsO, out int headerRow, out int lastRow);
      if (!header.TryGetValue("VARID", out int colVarId)
          || !header.TryGetValue("CHUNKINDEX", out int colChunkIndex)
          || !header.TryGetValue("CHUNKCOUNT", out int colChunkCount)
          || !header.TryGetValue("OBJECTTYPE", out int colObjectType)
          || !header.TryGetValue("PAYLOAD", out int colPayload))
      {
        return rows;
      }

      for (int r = headerRow + 1; r <= lastRow; r++)
      {
        int varId = GhExcelValueParsing.ToInt(wsO.Cell(r, colVarId).GetString());
        if (varId <= 0)
          continue;

        rows.Add(new GhExcelObjectSheet.ObjectChunkRow
        {
          VarId = varId,
          ChunkIndex = GhExcelValueParsing.ToInt(wsO.Cell(r, colChunkIndex).GetString()),
          ChunkCount = GhExcelValueParsing.ToInt(wsO.Cell(r, colChunkCount).GetString()),
          ObjectType = wsO.Cell(r, colObjectType).GetString() ?? "",
          Payload = wsO.Cell(r, colPayload).GetString() ?? ""
        });
      }

      return rows;
    }
  }
}
