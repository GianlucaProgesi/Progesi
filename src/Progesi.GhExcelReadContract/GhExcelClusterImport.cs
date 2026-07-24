using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ProgesiCore;

namespace Progesi.GhExcelReadContract
{
  /// <summary>
  /// Pure Excel cluster sheet → DTO helper (no Rhino/GH). Used by ImportExcelValidated.
  /// </summary>
  public sealed class GhExcelClusterRowDto
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int[] VariableIds { get; set; } = Array.Empty<int>();
    public string Hashtag { get; set; } = "";
    public string ParseWarning { get; set; } = "";
  }

  public static class GhExcelClusterImport
  {
    public static bool IsBlankDataRow(
      IXLWorksheet worksheet,
      int row,
      Dictionary<string, int> columnMap)
    {
      if (worksheet == null) throw new ArgumentNullException(nameof(worksheet));
      if (columnMap == null) throw new ArgumentNullException(nameof(columnMap));

      string id = GhExcelCellReader.ReadCell(worksheet, row, columnMap, "ID");
      string name = GhExcelCellReader.ReadCell(worksheet, row, columnMap, "NAME");
      string desc = GhExcelCellReader.ReadCell(worksheet, row, columnMap, "DESCRIPTION");
      string varIds = GhExcelCellReader.ReadCell(worksheet, row, columnMap, "VARIABLEIDS");
      string hash = GhExcelCellReader.ReadCell(worksheet, row, columnMap, "HASH");

      return GhExcelValueParsing.IsBlank(id)
          && GhExcelValueParsing.IsBlank(name)
          && GhExcelValueParsing.IsBlank(desc)
          && GhExcelValueParsing.IsBlank(varIds)
          && GhExcelValueParsing.IsBlank(hash);
    }

    /// <summary>
    /// Parses one cluster row via GhExcelClusterSheet + ClusterImportParser and builds a DTO
    /// with domain hashtag when VariableIds are present.
    /// </summary>
    public static bool TryBuildRowDto(
      IXLWorksheet worksheet,
      int row,
      Dictionary<string, int> columnMap,
      out GhExcelClusterRowDto dto,
      out string error)
    {
      dto = null;
      error = "";

      if (!GhExcelClusterSheet.TryParseClusterRow(
            worksheet,
            row,
            columnMap,
            out int id,
            out string name,
            out string description,
            out int[] variableIds,
            out string hash,
            out string warn))
      {
        error = string.IsNullOrWhiteSpace(warn) ? "cluster row parse failed" : warn;
        return false;
      }

      variableIds = variableIds ?? Array.Empty<int>();
      string hashtag = hash ?? "";

      if (variableIds.Length > 0)
      {
        var cluster = ProgesiVariableCluster.Rehydrate(
          id,
          name ?? "",
          variableIds,
          description,
          string.IsNullOrWhiteSpace(hash) ? null : hash);
        hashtag = cluster.Hashtag ?? "";
      }

      dto = new GhExcelClusterRowDto
      {
        Id = id,
        Name = name ?? "",
        Description = description ?? "",
        VariableIds = variableIds,
        Hashtag = hashtag ?? "",
        ParseWarning = warn ?? ""
      };

      return true;
    }

    /// <summary>
    /// Reads all non-blank cluster data rows from a worksheet (header row excluded).
    /// </summary>
    public static IReadOnlyList<GhExcelClusterRowDto> ReadAllRowDtos(
      IXLWorksheet worksheet,
      Dictionary<string, int> columnMap,
      int headerRow,
      int lastRow)
    {
      if (worksheet == null) throw new ArgumentNullException(nameof(worksheet));
      if (columnMap == null) throw new ArgumentNullException(nameof(columnMap));

      var rows = new List<GhExcelClusterRowDto>();
      for (int r = headerRow + 1; r <= lastRow; r++)
      {
        if (IsBlankDataRow(worksheet, r, columnMap))
          continue;

        if (!TryBuildRowDto(worksheet, r, columnMap, out var dto, out _))
          continue;

        rows.Add(dto);
      }

      return rows;
    }
  }
}
