using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerMetadataSheetTests
  {
    private static readonly string[] ExpectedHeaders =
    {
      "Id", "Hash", "Info", "By", "Ref", "LastModifiedUtc"
    };

    [Fact]
    public void Write_MetadataSheet_Uses_ProgesiMetadata_Name_And_Expected_Headers()
    {
      using var file = new ExcelTestFile();
      var metas = new[]
      {
        new ProgesiMetadataDto
        {
          Id = "10",
          Hash = "mh1",
          Info = "Project notes",
          By = "author",
          Ref = "ref-m",
          LastModifiedUtc = "2026-02-01T00:00:00Z"
        }
      };

      ExcelSerializer.Write(file.Path, System.Array.Empty<ProgesiVariableDto>(), metas, System.Array.Empty<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      wb.Worksheets.Should().Contain(ws => ws.Name == "ProgesiMetadata");

      var sheet = wb.Worksheet("ProgesiMetadata");
      sheet.Row(1).Cells(1, ExpectedHeaders.Length)
        .Select(c => c.GetString())
        .Should().Equal(ExpectedHeaders);

      sheet.Cell(2, 1).GetString().Should().Be("10");
      sheet.Cell(2, 3).GetString().Should().Be("Project notes");
      sheet.Cell(2, 4).GetString().Should().Be("author");
    }

    [Fact]
    public void Write_Empty_Metadata_List_Still_Writes_Header_Row()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());

      using var wb = new XLWorkbook(file.Path);
      var sheet = wb.Worksheet("ProgesiMetadata");
      sheet.LastRowUsed()!.RowNumber().Should().Be(1);
    }
  }
}
