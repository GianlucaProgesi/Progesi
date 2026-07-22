using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Progesi.DataExchange;
using Xunit;

namespace Progesi.DataExchange.Tests
{
  public class ExcelSerializerRoundTripTests
  {
    [Fact]
    public void RoundTrip_Variables_Preserves_Field_Values()
    {
      using var file = new ExcelTestFile();
      var original = new List<ProgesiVariableDto>
      {
        new ProgesiVariableDto
        {
          Id = "7",
          Hash = "vh7",
          Name = "Load",
          Value = "42",
          Unit = "kN",
          By = "eng",
          Ref = "r1",
          LastModifiedUtc = "2026-03-01T10:00:00Z"
        }
      };

      ExcelSerializer.Write(file.Path, original, new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());
      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Should().HaveCount(1);
      vars[0].Id.Should().Be("7");
      vars[0].Hash.Should().Be("vh7");
      vars[0].Name.Should().Be("Load");
      vars[0].Value.Should().Be("42");
      vars[0].Unit.Should().Be("kN");
      vars[0].By.Should().Be("eng");
      vars[0].Ref.Should().Be("r1");
      vars[0].LastModifiedUtc.Should().Be("2026-03-01T10:00:00Z");
    }

    [Fact]
    public void RoundTrip_Metadata_Preserves_Field_Values()
    {
      using var file = new ExcelTestFile();
      var original = new List<ProgesiMetadataDto>
      {
        new ProgesiMetadataDto
        {
          Id = "3",
          Hash = "mh3",
          Info = "Bridge span",
          By = "planner",
          Ref = "doc-3",
          LastModifiedUtc = "2026-04-01T12:00:00Z"
        }
      };

      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), original, new List<ProgesiAxisVariableDto>());
      var (_, mets, _) = ExcelSerializer.Read(file.Path);

      mets.Should().HaveCount(1);
      mets[0].Id.Should().Be("3");
      mets[0].Hash.Should().Be("mh3");
      mets[0].Info.Should().Be("Bridge span");
      mets[0].By.Should().Be("planner");
      mets[0].Ref.Should().Be("doc-3");
      mets[0].LastModifiedUtc.Should().Be("2026-04-01T12:00:00Z");
    }

    [Fact]
    public void RoundTrip_Variables_Preserves_Write_Order_Not_Sorted_By_Id()
    {
      using var file = new ExcelTestFile();
      var original = new List<ProgesiVariableDto>
      {
        new ProgesiVariableDto { Id = "30", Name = "Third", Value = "3" },
        new ProgesiVariableDto { Id = "10", Name = "First", Value = "1" },
        new ProgesiVariableDto { Id = "20", Name = "Second", Value = "2" }
      };

      ExcelSerializer.Write(file.Path, original, new List<ProgesiMetadataDto>(), new List<ProgesiAxisVariableDto>());
      var (vars, _, _) = ExcelSerializer.Read(file.Path);

      vars.Select(v => v.Id).Should().Equal("30", "10", "20");
      vars.Select(v => v.Name).Should().Equal("Third", "First", "Second");
    }

    [Fact]
    public void RoundTrip_Metadata_Preserves_Write_Order()
    {
      using var file = new ExcelTestFile();
      var original = new List<ProgesiMetadataDto>
      {
        new ProgesiMetadataDto { Id = "9", Info = "Z" },
        new ProgesiMetadataDto { Id = "1", Info = "A" },
        new ProgesiMetadataDto { Id = "5", Info = "M" }
      };

      ExcelSerializer.Write(file.Path, new List<ProgesiVariableDto>(), original, new List<ProgesiAxisVariableDto>());
      var (_, mets, _) = ExcelSerializer.Read(file.Path);

      mets.Select(m => m.Id).Should().Equal("9", "1", "5");
      mets.Select(m => m.Info).Should().Equal("Z", "A", "M");
    }

    [Fact]
    public void RoundTrip_Var_And_Meta_Together_Preserves_Both_Collections()
    {
      using var file = new ExcelTestFile();
      var vars = new List<ProgesiVariableDto>
      {
        new ProgesiVariableDto { Id = "1", Name = "V1", Value = "x" }
      };
      var mets = new List<ProgesiMetadataDto>
      {
        new ProgesiMetadataDto { Id = "2", Info = "M1" }
      };

      ExcelSerializer.Write(file.Path, vars, mets, new List<ProgesiAxisVariableDto>());
      var (readVars, readMets, axis) = ExcelSerializer.Read(file.Path);

      readVars.Should().HaveCount(1);
      readMets.Should().HaveCount(1);
      readVars[0].Name.Should().Be("V1");
      readMets[0].Info.Should().Be("M1");
      axis.Should().BeEmpty();
    }
  }
}
