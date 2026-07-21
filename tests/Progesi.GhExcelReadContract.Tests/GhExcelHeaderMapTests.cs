using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelHeaderMapTests
  {
    [Fact]
    public void Build_Normalizes_Header_Keys_And_Returns_Row_Bounds()
    {
      using var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add("Sheet1");
      ws.Cell(1, 1).Value = "Var Name";
      ws.Cell(1, 2).Value = "meta-id";
      ws.Cell(2, 1).Value = "A";
      ws.Cell(3, 2).Value = "B";

      var map = GhExcelHeaderMap.Build(ws, out var firstRow, out var lastRow);

      firstRow.Should().Be(1);
      lastRow.Should().Be(3);
      map.Should().ContainKey("VARNAME");
      map.Should().ContainKey("METAID");
      map["VARNAME"].Should().Be(1);
      map["METAID"].Should().Be(2);
    }

    [Fact]
    public void Build_Empty_Sheet_Returns_Empty_Map()
    {
      using var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add("Empty");

      var map = GhExcelHeaderMap.Build(ws, out var firstRow, out var lastRow);

      firstRow.Should().Be(1);
      lastRow.Should().Be(0);
      map.Should().BeEmpty();
    }
  }
}
