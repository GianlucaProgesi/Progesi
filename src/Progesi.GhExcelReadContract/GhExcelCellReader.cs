using System.Collections.Generic;
using ClosedXML.Excel;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelCellReader
  {
    public static string ReadCell(IXLWorksheet worksheet, int row, Dictionary<string, int> map, string key)
    {
      if (!map.TryGetValue(key, out int column))
        return string.Empty;

      var cell = worksheet.Cell(row, column);

      var text = cell.GetString();
      if (!string.IsNullOrWhiteSpace(text))
        return text;

      text = cell.GetFormattedString();
      if (!string.IsNullOrWhiteSpace(text))
        return text;

      try { return cell.Value.ToString(); }
      catch { return string.Empty; }
    }
  }
}
