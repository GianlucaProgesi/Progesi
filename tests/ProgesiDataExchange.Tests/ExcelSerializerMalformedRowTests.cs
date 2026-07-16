using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerMalformedRowTests
  {
    [Fact]
    public void Read_Variable_Row_With_Only_Id_Fills_Remaining_Fields_With_Empty_Strings()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Hash";
        ws.Cell(1, 3).Value = "Name";
        ws.Cell(1, 4).Value = "Value";
        ws.Cell(2, 1).Value = "42";
        wb.SaveAs(file.Path);
      }

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(1);
      vars[0].Id.Should().Be("42");
      vars[0].Hash.Should().BeEmpty();
      vars[0].Name.Should().BeEmpty();
      vars[0].Value.Should().BeEmpty();
    }

    [Fact]
    public void Read_Variable_Sheet_Ignores_Unknown_Header_Columns()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "ExtraColumn";
        ws.Cell(1, 3).Value = "Name";
        ws.Cell(2, 1).Value = "1";
        ws.Cell(2, 2).Value = "ignored";
        ws.Cell(2, 3).Value = "Width";
        wb.SaveAs(file.Path);
      }

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(1);
      vars[0].Id.Should().Be("1");
      vars[0].Name.Should().Be("Width");
    }

    [Fact]
    public void Read_Variable_Sheet_With_Reordered_Columns_Maps_By_Header_Name()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Id";
        ws.Cell(1, 3).Value = "Value";
        ws.Cell(2, 1).Value = "Depth";
        ws.Cell(2, 2).Value = "9";
        ws.Cell(2, 3).Value = "3.5";
        wb.SaveAs(file.Path);
      }

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(1);
      vars[0].Id.Should().Be("9");
      vars[0].Name.Should().Be("Depth");
      vars[0].Value.Should().Be("3.5");
    }

    [Fact]
    public void Read_Variable_Sheet_Includes_Blank_Data_Row_As_Default_Dto()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(3, 1).Value = "2";
        ws.Cell(3, 2).Value = "AfterBlank";
        wb.SaveAs(file.Path);
      }

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(2);
      vars[0].Id.Should().BeEmpty();
      vars[0].Name.Should().BeEmpty();
      vars[1].Id.Should().Be("2");
      vars[1].Name.Should().Be("AfterBlank");
    }

    [Fact]
    public void Read_Metadata_Row_With_Partial_Columns_Preserves_Provided_Fields()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiMetadata");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Info";
        ws.Cell(1, 3).Value = "By";
        ws.Cell(2, 2).Value = "notes-only";
        wb.SaveAs(file.Path);
      }

      var (_, mets, _) = ExcelSerializer.Read(file.Path);

      mets.Should().HaveCount(1);
      mets[0].Id.Should().BeEmpty();
      mets[0].Info.Should().Be("notes-only");
      mets[0].By.Should().BeEmpty();
    }

    [Fact]
    public void Read_Metadata_Sheet_Ignores_Unknown_Header_Columns()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiMetadata");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Unknown";
        ws.Cell(1, 3).Value = "Info";
        ws.Cell(2, 1).Value = "5";
        ws.Cell(2, 2).Value = "drop-me";
        ws.Cell(2, 3).Value = "kept";
        wb.SaveAs(file.Path);
      }

      var (_, mets, _) = ExcelSerializer.Read(file.Path);

      mets.Should().HaveCount(1);
      mets[0].Id.Should().Be("5");
      mets[0].Info.Should().Be("kept");
    }

    [Fact]
    public void Read_Hand_Built_Workbook_Preserves_Row_Order_On_Import()
    {
      using var file = new ExcelTestFile();
      using (var wb = new XLWorkbook())
      {
        var ws = wb.Worksheets.Add("ProgesiVariable");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(2, 1).Value = "100";
        ws.Cell(2, 2).Value = "RowA";
        ws.Cell(3, 1).Value = "50";
        ws.Cell(3, 2).Value = "RowB";
        wb.SaveAs(file.Path);
      }

      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Select(v => v.Id).Should().Equal("100", "50");
      vars.Select(v => v.Name).Should().Equal("RowA", "RowB");
    }
  }
}
