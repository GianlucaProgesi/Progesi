using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerWorkbookShapeTests
  {
    [Fact]
    public void Write_Always_Creates_Three_Progesi_Sheets_With_Expected_Names()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(
        file.Path,
        new List<ProgesiVariableDto>(),
        new List<ProgesiMetadataDto>(),
        new List<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      wb.Worksheets.Select(ws => ws.Name).Should().Equal(
        "ProgesiVariable",
        "ProgesiMetadata",
        "ProgesiAxisVariable");
    }

    [Fact]
    public void Write_Empty_Axis_Collection_Still_Writes_ProgesiAxisVariable_Header_Row()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(
        file.Path,
        new List<ProgesiVariableDto>(),
        new List<ProgesiMetadataDto>(),
        new List<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      var sheet = wb.Worksheet("ProgesiAxisVariable");
      sheet.Cell(1, 1).GetString().Should().Be("Id");
      sheet.Cell(1, 4).GetString().Should().Be("ValueTypeKey");
      sheet.LastRowUsed()!.RowNumber().Should().Be(1);
    }

    [Fact]
    public void Read_Workbook_With_Unrelated_Sheet_Only_Returns_Empty_Collections()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        wb.Worksheets.Add("OtherSheet");
        wb.SaveAs(file.Path);
      }

      var (vars, mets, axis) = ExcelSerializer.Read(file.Path);

      vars.Should().BeEmpty();
      mets.Should().BeEmpty();
      axis.Should().BeEmpty();
    }
  }
}
