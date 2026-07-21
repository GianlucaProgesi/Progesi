using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelAliasMapsTests
  {
    [Fact]
    public void NormalizeKey_Strips_Non_Alphanumeric()
    {
      GhExcelAliasMaps.NormalizeKey(" meta-id ").Should().Be("METAID");
      GhExcelAliasMaps.NormalizeKey("Var Name").Should().Be("VARNAME");
    }

    [Fact]
    public void Build_Merges_Custom_Variable_Aliases_From_MapJson()
    {
      const string mapJson = @"{ ""Variables"": { ""NAME"": [""FieldName""] } }";

      var (variableAliases, _) = GhExcelAliasMaps.Build(mapJson);

      variableAliases["NAME"].Should().Contain("FIELDNAME");
      variableAliases["NAME"].Should().Contain("NAME");
    }

    [Fact]
    public void Build_Ignores_Malformed_MapJson()
    {
      var (variableAliases, metadataAliases) = GhExcelAliasMaps.Build("{ not-json");

      variableAliases.Should().ContainKey("NAME");
      metadataAliases.Should().ContainKey("BY");
    }

    [Fact]
    public void CreateDefaultClusterAliases_Includes_VariableIds()
    {
      var aliases = GhExcelAliasMaps.CreateDefaultClusterAliases();

      aliases.Should().ContainKey("VARIABLEIDS");
      aliases["VARIABLEIDS"].Should().Contain("VARIDS");
    }
  }
}
