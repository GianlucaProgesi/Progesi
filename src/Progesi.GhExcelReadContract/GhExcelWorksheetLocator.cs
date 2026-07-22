using System;
using ClosedXML.Excel;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelWorksheetLocator
  {
    public static IXLWorksheet TryGetWorksheet(IXLWorkbook workbook, params string[] names)
    {
      if (workbook == null) throw new ArgumentNullException(nameof(workbook));
      if (names == null || names.Length == 0) return null;

      foreach (var name in names)
      {
        foreach (var worksheet in workbook.Worksheets)
        {
          if (string.Equals(worksheet.Name, name, StringComparison.OrdinalIgnoreCase))
            return worksheet;
        }
      }

      return null;
    }
  }
}
