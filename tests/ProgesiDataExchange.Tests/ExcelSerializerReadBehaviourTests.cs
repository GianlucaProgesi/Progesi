using System.Collections.Generic;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerReadBehaviourTests
  {
    [Fact]
    public void Read_Missing_Variable_Sheet_Returns_Empty_Variable_List()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiMetadata");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Hash";
        ws.Cell(1, 3).Value = "Info";
        ws.Cell(2, 1).Value = "1";
        ws.Cell(2, 3).Value = "only-meta";
        wb.SaveAs(file.Path);
      }

      var (vars, mets, axis) = ExcelSerializer.Read(file.Path);

      vars.Should().BeEmpty();
      mets.Should().HaveCount(1);
      mets[0].Info.Should().Be("only-meta");
      axis.Should().BeEmpty();
    }

    [Fact]
    public void Read_Missing_Metadata_Sheet_Returns_Empty_Metadata_List()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Hash";
        ws.Cell(1, 3).Value = "Name";
        ws.Cell(2, 1).Value = "1";
        ws.Cell(2, 3).Value = "only-var";
        wb.SaveAs(file.Path);
      }

      var (vars, mets, axis) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(1);
      vars[0].Name.Should().Be("only-var");
      mets.Should().BeEmpty();
      axis.Should().BeEmpty();
    }

    [Fact]
    public void Read_HeaderOnly_Variable_Sheet_Returns_Empty_Variable_List()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().BeEmpty();
    }

    [Fact]
    public void Read_HeaderOnly_Metadata_Sheet_Returns_Empty_Metadata_List()
    {
      using var file = new ExcelTestFile();
      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());

      var (_, mets, _) = ExcelSerializer.Read(file.Path);

      mets.Should().BeEmpty();
    }
  }
}
