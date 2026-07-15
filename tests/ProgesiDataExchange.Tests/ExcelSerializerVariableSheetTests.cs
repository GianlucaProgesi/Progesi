using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerVariableSheetTests
  {
    private static readonly string[] ExpectedHeaders =
    {
      "Id", "Hash", "Name", "Value", "Unit", "By", "Ref", "LastModifiedUtc"
    };

    [Fact]
    public void Write_VariableSheet_Uses_ProgesiVariable_Name_And_Expected_Headers()
    {
      using var file = new ExcelTestFile();
      var vars = new[]
      {
        new ProgesiVariableDto
        {
          Id = "1",
          Hash = "h1",
          Name = "Width",
          Value = "12.5",
          Unit = "m",
          By = "tester",
          Ref = "ref-a",
          LastModifiedUtc = "2026-01-01T00:00:00Z"
        }
      };

      ExcelSerializer.Write(file.Path, vars, System.Array.Empty<ProgesiMetadataDto>(), System.Array.Empty<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      wb.Worksheets.Should().Contain(ws => ws.Name == "ProgesiVariable");

      var sheet = wb.Worksheet("ProgesiVariable");
      sheet.Cell(1, 1).GetString().Should().Be("Id");
      sheet.Row(1).Cells(1, ExpectedHeaders.Length)
        .Select(c => c.GetString())
        .Should().Equal(ExpectedHeaders);

      sheet.Cell(2, 1).GetString().Should().Be("1");
      sheet.Cell(2, 3).GetString().Should().Be("Width");
      sheet.Cell(2, 4).GetString().Should().Be("12.5");
    }

    [Fact]
    public void Write_Empty_Variable_List_Still_Writes_Header_Row()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      var sheet = wb.Worksheet("ProgesiVariable");
      sheet.LastRowUsed()!.RowNumber().Should().Be(1);
    }
  }
}
