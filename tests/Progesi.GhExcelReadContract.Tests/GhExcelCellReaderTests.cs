using System.Collections.Generic;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelCellReaderTests
  {
    [Fact]
    public void ReadCell_Uses_GetString_Then_GetFormattedString_Fallback()
    {
      using var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add("Sheet1");
      ws.Cell(2, 1).Value = 42;
      ws.Cell(2, 1).Style.NumberFormat.Format = "0.00";

      var map = new Dictionary<string, int> { ["VALUE"] = 1 };
      var text = GhExcelCellReader.ReadCell(ws, 2, map, "VALUE");

      text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReadCell_Returns_Empty_When_Column_Not_Mapped()
    {
      using var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add("Sheet1");
      ws.Cell(2, 1).Value = "x";

      GhExcelCellReader.ReadCell(ws, 2, new Dictionary<string, int>(), "NAME").Should().BeEmpty();
    }
  }
}
