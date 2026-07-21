using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using ProgesiCore;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelClusterSheet
  {
    public static bool TryParseClusterRow(
      IXLWorksheet worksheet,
      int row,
      Dictionary<string, int> columnMap,
      out int id,
      out string name,
      out string description,
      out int[] variableIds,
      out string hash,
      out string warn)
    {
      if (worksheet == null) throw new ArgumentNullException(nameof(worksheet));
      if (columnMap == null) throw new ArgumentNullException(nameof(columnMap));

      return ClusterImportParser.TryParseClusterRow(
        key => GhExcelCellReader.ReadCell(worksheet, row, columnMap, key),
        out id,
        out name,
        out description,
        out variableIds,
        out hash,
        out warn);
    }

    public static Dictionary<string, int> ResolveClusterColumns(Dictionary<string, int> header)
    {
      return GhExcelColumnMap.ResolveColumns(header, GhExcelAliasMaps.CreateDefaultClusterAliases());
    }
  }
}
