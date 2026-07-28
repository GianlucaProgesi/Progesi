using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using Progesi.GhExcelReadContract;

namespace Progesi.GhExcelReadContract.Tests.Support
{
  internal static class GhExcelWorkbookReader
  {
    internal static CanonicalExchangeModel Read(XLWorkbook workbook, string mapJson = null)
    {
      if (workbook == null) throw new ArgumentNullException(nameof(workbook));

      var (varAliases, metaAliases) = GhExcelAliasMaps.Build(mapJson);
      var clusterAliases = GhExcelAliasMaps.CreateDefaultClusterAliases();
      var objectChunks = ReadObjectChunkRows(workbook);

      return new CanonicalExchangeModel
      {
        Variables = ReadVariables(workbook, varAliases, objectChunks),
        Metadata = ReadMetadata(workbook, metaAliases),
        Clusters = ReadClusters(workbook, clusterAliases)
      };
    }

    internal static IReadOnlyList<VariableRow> ReadVariables(
      XLWorkbook workbook,
      Dictionary<string, HashSet<string>> varAliases,
      IReadOnlyList<GhExcelObjectSheet.ObjectChunkRow> objectChunks)
    {
      var ws = GhExcelWorksheetLocator.TryGetWorksheet(
        workbook,
        GhExcelSheetNames.Variables,
        GhExcelSheetNames.VariablesAlias);
      if (ws == null)
        return Array.Empty<VariableRow>();

      var header = GhExcelHeaderMap.Build(ws, out int headerRow, out int lastRow);
      var map = GhExcelColumnMap.ResolveColumns(header, varAliases);
      var rows = new List<VariableRow>();

      for (int r = headerRow + 1; r <= lastRow; r++)
      {
        string name = GhExcelCellReader.ReadCell(ws, r, map, "NAME");
        string value = GhExcelCellReader.ReadCell(ws, r, map, "VALUE");
        string deps = GhExcelCellReader.ReadCell(ws, r, map, "DEPENDS");
        string asS = GhExcelCellReader.ReadCell(ws, r, map, "ASSUMPTION");
        int id = GhExcelValueParsing.ToInt(GhExcelCellReader.ReadCell(ws, r, map, "ID"));
        string metaCell = GhExcelCellReader.ReadCell(ws, r, map, "METAID");

        if (GhExcelValueParsing.IsBlank(name)
            && GhExcelValueParsing.IsBlank(value)
            && GhExcelValueParsing.IsBlank(deps)
            && GhExcelValueParsing.IsBlank(asS))
          continue;

        var row = new VariableRow
        {
          Id = id,
          Hash = GhExcelCellReader.ReadCell(ws, r, map, "HASH"),
          Name = name,
          Value = value,
          ValC = GhExcelCellReader.ReadCell(ws, r, map, "VALC"),
          MetaIds = ParseMetadataIds(metaCell),
          Depends = GhExcelValueParsing.ParseDepends(deps),
          Assumption = GhExcelValueParsing.ToBool(asS)
        };

        if (GhExcelObjectSheet.TryParseObjectMarker(value, out var objectType))
        {
          row.ObjectType = objectType;
          if (GhExcelObjectSheet.TryReassemblePayload(objectChunks, id, out var payload, out var rebuiltType, out _))
          {
            row.ObjectPayloadJson = payload;
            row.ObjectType = rebuiltType;
          }
        }

        rows.Add(row);
      }

      return rows;
    }

    internal static IReadOnlyList<MetadataRow> ReadMetadata(
      XLWorkbook workbook,
      Dictionary<string, HashSet<string>> metaAliases)
    {
      var ws = GhExcelWorksheetLocator.TryGetWorksheet(
        workbook,
        GhExcelSheetNames.Metadata,
        GhExcelSheetNames.MetadataAlias);
      if (ws == null)
        return Array.Empty<MetadataRow>();

      var header = GhExcelHeaderMap.Build(ws, out int headerRow, out int lastRow);
      var map = GhExcelColumnMap.ResolveColumns(header, metaAliases);
      var rows = new List<MetadataRow>();

      for (int r = headerRow + 1; r <= lastRow; r++)
      {
        string by = GhExcelCellReader.ReadCell(ws, r, map, "BY");
        string desc = GhExcelCellReader.ReadCell(ws, r, map, "DESCRIPTION");
        string refs = GhExcelCellReader.ReadCell(ws, r, map, "REFS");
        int id = GhExcelValueParsing.ToInt(GhExcelCellReader.ReadCell(ws, r, map, "ID"));

        if (GhExcelValueParsing.IsBlank(by) && GhExcelValueParsing.IsBlank(desc) && GhExcelValueParsing.IsBlank(refs))
          continue;

        rows.Add(new MetadataRow
        {
          Id = id,
          Hash = GhExcelCellReader.ReadCell(ws, r, map, "HASH"),
          By = by,
          Description = desc,
          Refs = ParseRefs(refs),
          LastModified = GhExcelCellReader.ReadCell(ws, r, map, "LM")
        });
      }

      return rows;
    }

    internal static IReadOnlyList<ClusterRow> ReadClusters(
      XLWorkbook workbook,
      Dictionary<string, HashSet<string>> clusterAliases)
    {
      var ws = GhExcelWorksheetLocator.TryGetWorksheet(
        workbook,
        GhExcelSheetNames.Clusters,
        GhExcelSheetNames.ClustersAlias);
      if (ws == null)
        return Array.Empty<ClusterRow>();

      var header = GhExcelHeaderMap.Build(ws, out int headerRow, out int lastRow);
      var map = GhExcelColumnMap.ResolveColumns(header, clusterAliases);
      var rows = new List<ClusterRow>();

      for (int r = headerRow + 1; r <= lastRow; r++)
      {
        if (GhExcelClusterImport.IsBlankDataRow(ws, r, map))
          continue;

        if (!GhExcelClusterImport.TryBuildRowDto(ws, r, map, out var dto, out _))
          continue;

        rows.Add(new ClusterRow
        {
          Id = dto.Id,
          Hash = dto.Hashtag,
          Name = dto.Name,
          Description = dto.Description,
          VariableIds = dto.VariableIds ?? Array.Empty<int>()
        });
      }

      return rows;
    }

    internal static List<GhExcelObjectSheet.ObjectChunkRow> ReadObjectChunkRows(XLWorkbook workbook)
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

    internal static int[] ParseMetadataIds(string cellValue)
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

    private static string[] ParseRefs(string refsCell)
    {
      if (string.IsNullOrWhiteSpace(refsCell))
        return Array.Empty<string>();

      return refsCell
        .Split('|')
        .Select(s => s?.Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .ToArray();
    }
  }
}
