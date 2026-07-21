using System;
using System.Collections.Generic;
using ClosedXML.Excel;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelHeaderMap
  {
    public static Dictionary<string, int> Build(IXLWorksheet worksheet, out int firstRow, out int lastRow)
    {
      if (worksheet == null) throw new ArgumentNullException(nameof(worksheet));

      var used = worksheet.RangeUsed();
      if (used == null)
      {
        firstRow = 1;
        lastRow = 0;
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      }

      firstRow = used.RangeAddress.FirstAddress.RowNumber;
      lastRow = used.RangeAddress.LastAddress.RowNumber;

      var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var headerRow = worksheet.Row(firstRow);
      foreach (var cell in headerRow.CellsUsed())
      {
        var key = GhExcelAliasMaps.NormalizeKey(cell.GetString());
        if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
          map[key] = cell.Address.ColumnNumber;
      }

      return map;
    }
  }
}
