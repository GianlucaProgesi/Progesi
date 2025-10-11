using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ProgesiCore;
using ProgesiCore.Serialization;

namespace ProgesiCore.Tests
{
  public class ProgesiAxisVariableDtoTests
  {
    private static ProgesiAxisVariable MakeAxis()
    {
      var axis = new ProgesiAxisVariable(7, "AX-DTO", 12.5, 99);
      axis.Add("A", 0.0, 1);
      axis.Add("A", 1.25, 2);
      axis.Add("B", 1.25, 10);
      axis.Add("B", 6.0, 11);
      return axis;
    }

    [Fact]
    public void FromDomain_ToDomain_RoundTrip_EqualState()
    {
      var original = MakeAxis();

      var dto = ProgesiAxisVariableDto.FromDomain(original);
      var rebuilt = ProgesiAxisVariableDto.ToDomain(dto);

      Assert.True(original.Equals(rebuilt));
      Assert.Equal(original.GetHashCode(), rebuilt.GetHashCode());
    }

    [Fact]
    public void FromDomain_ProducesFlatEntries_CoveringEnumerateAll()
    {
      var axis = MakeAxis();
      var triples = axis.EnumerateAll()
                        .Select(t => $"{t.variableName}|{t.position:F6}|{t.variableId}")
                        .OrderBy(s => s)
                        .ToArray();

      var dto = ProgesiAxisVariableDto.FromDomain(axis);
      var flat = dto.EnumerateFlat()
                    .Select(t => $"{t.VariableName}|{t.Position:F6}|{t.VariableId}")
                    .OrderBy(s => s)
                    .ToArray();

      Assert.Equal(triples, flat);
    }

    [Fact]
    public void ToDomain_ValidatesContext_AndEntries()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 1,
        AxisName = "AX",
        AxisLength = 5.0,
        RuleId = 1,
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      // ok
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = 0.0, VariableId = 1 });
      var ok = ProgesiAxisVariableDto.ToDomain(dto);
      Assert.Contains(1, ok.GetAt("V", 0.0));

      // posizione fuori range → eccezione
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = 10.0, VariableId = 2 });
      Assert.Throws<ArgumentOutOfRangeException>(() => ProgesiAxisVariableDto.ToDomain(dto));
    }

    [Fact]
    public void ToDomain_RebucketsPositions_ByTolerance()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 2,
        AxisName = "AX",
        AxisLength = null,
        RuleId = null,
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      double tol = ProgesiAxisVariable.DefaultTolerance;
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = 1.0, VariableId = 1 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = 1.0 + tol * 0.4, VariableId = 2 });

      var axis = ProgesiAxisVariableDto.ToDomain(dto, tol);
      var at = axis.GetAt("V", 1.0).OrderBy(x => x).ToArray();
      Assert.Equal(new[] { 1, 2 }, at);
    }

    [Fact]
    public void ToDomain_RejectsInvalidHeaders()
    {
      var good = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX" };
      Assert.NotNull(ProgesiAxisVariableDto.ToDomain(good));

      var bad1 = new ProgesiAxisVariableDto { AxisId = -1, AxisName = "AX" };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad1));

      var bad2 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = null! };
      Assert.Throws<ArgumentNullException>(() => ProgesiAxisVariableDto.ToDomain(bad2));

      var bad3 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", AxisLength = 0.0 };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad3));

      var bad4 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", RuleId = -5 };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad4));
    }

    [Fact]
    public void ToDomain_RejectsInvalidEntries()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 1,
        AxisName = "AX",
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = null!, Position = 0.0, VariableId = 1 });
      Assert.Throws<ArgumentNullException>(() => ProgesiAxisVariableDto.ToDomain(dto));

      dto.Entries.Clear();
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = double.NaN, VariableId = 1 });
      Assert.Throws<ArgumentOutOfRangeException>(() => ProgesiAxisVariableDto.ToDomain(dto));

      dto.Entries.Clear();
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "V", Position = 0.0, VariableId = -1 });
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(dto));
    }

    [Fact]
    public void RoundTrip_OrderIndependenceOnEntries()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 5,
        AxisName = "AX",
        AxisLength = 10.0,
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      // Inseriamo in ordine “strano”
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "B", Position = 2.0, VariableId = 9 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "A", Position = 1.0, VariableId = 1 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { VariableName = "A", Position = 1.0, VariableId = 2 });

      var axis = ProgesiAxisVariableDto.ToDomain(dto);
      var dto2 = ProgesiAxisVariableDto.FromDomain(axis);

      // Confronto come multinsieme (ordinato)
      Func<ProgesiAxisVariableDto.Entry, string> key = e => $"{e.VariableName}|{e.Position:F6}|{e.VariableId}";
      var a = dto.Entries.Select(key).OrderBy(x => x).ToArray();
      var b = dto2.Entries.Select(key).OrderBy(x => x).ToArray();
      Assert.Equal(a, b);
    }
  }
}
