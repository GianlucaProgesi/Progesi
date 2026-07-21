using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelColumnMapTests
  {
    [Fact]
    public void ResolveColumns_Uses_Alias_When_Canonical_Header_Missing()
    {
      var header = new Dictionary<string, int> { ["VALORE"] = 4 };
      var aliases = GhExcelAliasMaps.CreateDefaultVariableAliases();

      var resolved = GhExcelColumnMap.ResolveColumns(header, aliases);

      resolved["VALUE"].Should().Be(4);
    }

    [Fact]
    public void MissingRequired_Reports_Absent_Headers()
    {
      var map = new Dictionary<string, int> { ["NAME"] = 2 };

      var missing = GhExcelColumnMap.MissingRequired(map, new[] { "NAME", "VALUE", "BY" });

      missing.Should().Equal("VALUE", "BY");
    }
  }
}
