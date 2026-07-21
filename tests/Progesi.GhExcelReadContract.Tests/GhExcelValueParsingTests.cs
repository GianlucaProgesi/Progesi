using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelValueParsingTests
  {
    [Theory]
    [InlineData("1, 3;2|4", new[] { 1, 2, 3, 4 })]
    [InlineData("", new int[0])]
    [InlineData("0,-1,abc", new int[0])]
    public void ParseDepends_Parses_And_Sorts_Positive_Ids(string raw, int[] expected)
    {
      GhExcelValueParsing.ParseDepends(raw).Should().Equal(expected);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void ToBool_Matches_GH_Contract(string raw, bool expected)
    {
      GhExcelValueParsing.ToBool(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("bad", 0)]
    [InlineData(" 7 ", 7)]
    public void ToInt_Parses_Integer_Or_Zero(string raw, int expected)
    {
      GhExcelValueParsing.ToInt(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(" ", true)]
    [InlineData("x", false)]
    public void IsBlank_Matches_Whitespace_Rules(string raw, bool expected)
    {
      GhExcelValueParsing.IsBlank(raw).Should().Be(expected);
    }
  }
}
