using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelClusterImportTests
  {
    private static (IXLWorksheet ws, Dictionary<string, int> columns, int headerRow, int lastRow) BuildSheet()
    {
      var wb = new XLWorkbook();
      var ws = wb.Worksheets.Add(GhExcelSheetNames.Clusters);
      ws.Cell(1, 1).Value = "Id";
      ws.Cell(1, 2).Value = "Name";
      ws.Cell(1, 3).Value = "Description";
      ws.Cell(1, 4).Value = "Hash";
      ws.Cell(1, 5).Value = "VariableIds";
      ws.Cell(2, 1).Value = 7;
      ws.Cell(2, 2).Value = "SpanSet";
      ws.Cell(2, 3).Value = "Notes";
      ws.Cell(2, 4).Value = "h7";
      ws.Cell(2, 5).Value = "3,4";
      ws.Cell(3, 1).Value = 8;
      ws.Cell(3, 2).Value = "EmptyMembers";
      ws.Cell(3, 5).Value = "";

      var header = GhExcelHeaderMap.Build(ws, out int headerRow, out int lastRow);
      var columns = GhExcelClusterSheet.ResolveClusterColumns(header);
      return (ws, columns, headerRow, lastRow);
    }

    [Fact]
    public void TryBuildRowDto_Uses_Sheet_Hash_When_Provided()
    {
      var (ws, columns, _, _) = BuildSheet();

      var ok = GhExcelClusterImport.TryBuildRowDto(ws, 2, columns, out var dto, out var error);

      ok.Should().BeTrue();
      error.Should().BeEmpty();
      dto.Id.Should().Be(7);
      dto.Name.Should().Be("SpanSet");
      dto.Description.Should().Be("Notes");
      dto.VariableIds.Should().Equal(3, 4);
      dto.Hashtag.Should().Be("h7");
      dto.ParseWarning.Should().BeEmpty();
    }

    [Fact]
    public void TryBuildRowDto_Computes_Domain_Hashtag_When_Sheet_Hash_Missing()
    {
      var (ws, columns, _, _) = BuildSheet();
      ws.Cell(2, 4).Value = "";

      var ok = GhExcelClusterImport.TryBuildRowDto(ws, 2, columns, out var dto, out var error);

      ok.Should().BeTrue();
      var expected = ProgesiVariableCluster.Rehydrate(7, "SpanSet", new[] { 3, 4 }, "Notes");
      dto.Hashtag.Should().Be(expected.Hashtag);
      dto.Hashtag.Should().Be(ProgesiHash.Compute(expected));
    }

    [Fact]
    public void TryBuildRowDto_Allows_Empty_VariableIds_With_Sheet_Hash()
    {
      var (ws, columns, _, _) = BuildSheet();

      var ok = GhExcelClusterImport.TryBuildRowDto(ws, 3, columns, out var dto, out var error);

      ok.Should().BeTrue();
      error.Should().BeEmpty();
      dto.Id.Should().Be(8);
      dto.VariableIds.Should().BeEmpty();
      dto.Hashtag.Should().BeEmpty();
    }

    [Fact]
    public void IsBlankDataRow_Skips_Fully_Empty_Row()
    {
      var (ws, columns, _, _) = BuildSheet();
      ws.Cell(4, 1).Value = "";
      ws.Cell(4, 2).Value = "";
      ws.Cell(4, 3).Value = "";
      ws.Cell(4, 4).Value = "";
      ws.Cell(4, 5).Value = "";

      GhExcelClusterImport.IsBlankDataRow(ws, 4, columns).Should().BeTrue();
      GhExcelClusterImport.IsBlankDataRow(ws, 2, columns).Should().BeFalse();
    }

    [Fact]
    public void ReadAllRowDtos_Returns_NonBlank_Parsed_Rows()
    {
      var (ws, columns, headerRow, lastRow) = BuildSheet();

      var rows = GhExcelClusterImport.ReadAllRowDtos(ws, columns, headerRow, lastRow);

      rows.Should().HaveCount(2);
      rows[0].Id.Should().Be(7);
      rows[1].Id.Should().Be(8);
    }

    [Fact]
    public void TryBuildRowDto_Fails_On_Invalid_Id()
    {
      var (ws, columns, _, _) = BuildSheet();
      ws.Cell(5, 1).Value = "abc";
      ws.Cell(5, 2).Value = "Bad";

      var ok = GhExcelClusterImport.TryBuildRowDto(ws, 5, columns, out _, out var error);

      ok.Should().BeFalse();
      error.Should().Contain("invalid Id");
    }
  }
}
