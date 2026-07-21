using ClosedXML.Excel;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelClusterSheetTests
  {
    [Fact]
    public void TryParseClusterRow_Reads_ProgesiClusters_Row_Through_ClosedXML()
    {
      using var wb = new XLWorkbook();
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

      var header = GhExcelHeaderMap.Build(ws, out _, out _);
      var columns = GhExcelClusterSheet.ResolveClusterColumns(header);

      var ok = GhExcelClusterSheet.TryParseClusterRow(
        ws,
        2,
        columns,
        out var id,
        out var name,
        out var description,
        out var variableIds,
        out var hash,
        out var warn);

      ok.Should().BeTrue();
      id.Should().Be(7);
      name.Should().Be("SpanSet");
      description.Should().Be("Notes");
      hash.Should().Be("h7");
      variableIds.Should().Equal(3, 4);
      warn.Should().BeEmpty();
    }

    [Fact]
    public void ResolveClusterColumns_Maps_VarIds_Alias()
    {
      var header = new System.Collections.Generic.Dictionary<string, int>
      {
        ["VARIDS"] = 5
      };

      var columns = GhExcelClusterSheet.ResolveClusterColumns(header);

      columns["VARIABLEIDS"].Should().Be(5);
    }
  }
}
