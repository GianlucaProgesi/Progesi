using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ClusterImportParserTests
  {
    [Theory]
    [InlineData("3,4", new[] { 3, 4 })]
    [InlineData("3.4", new[] { 3, 4 })]          // Excel decimal -> separatore
    [InlineData("3,4.5", new[] { 3, 4, 5 })]     // misto
    [InlineData("1!5", new[] { 1, 5 })]
    [InlineData("1|5", new[] { 1, 5 })]
    [InlineData(" 1  5 ", new[] { 1, 5 })]
    [InlineData("3,\u00A04", new[] { 3, 4 })]    // NBSP
    public void ParseVariableIds_Works(string raw, int[] expected)
    {
      var ids = ClusterImportParser.ParseVariableIds(raw);
      Assert.Equal(expected, ids);
    }

    [Fact]
    public void TryParseClusterRow_Parses_Row()
    {
      string Cell(string key)
      {
        switch (key)
        {
          case "ID": return "5";
          case "NAME": return "Alfio";
          case "DESCRIPTION": return "Ciccio";
          case "HASH": return "5|Alfio|3,4";
          case "VARIABLEIDS": return "3.4";
          default: return "";
        }
      }

      var ok = ClusterImportParser.TryParseClusterRow(
        Cell,
        out var id,
        out var name,
        out var desc,
        out var varIds,
        out var hash,
        out var warn);

      Assert.True(ok);
      Assert.Equal(5, id);
      Assert.Equal("Alfio", name);
      Assert.Equal("Ciccio", desc);
      Assert.Equal("5|Alfio|3,4", hash);
      Assert.Equal(new[] { 3, 4 }, varIds);
      Assert.True(string.IsNullOrEmpty(warn));
    }
  }
}
