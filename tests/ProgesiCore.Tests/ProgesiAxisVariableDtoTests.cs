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
      var axis = new ProgesiAxisVariable(
        id: 7,
        axisName: "AX-DTO",
        name: "Thickness",
        valueTypeKey: "System.Double",
        axisLength: 12.5,
        ruleId: 99);

      var s1 = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double");
      var s2 = new ProgesiAxisVariable.ProgesiVariableSignature(2, "Thickness", "System.Double");
      var s3 = new ProgesiAxisVariable.ProgesiVariableSignature(10, "Thickness", "System.Double");
      var s4 = new ProgesiAxisVariable.ProgesiVariableSignature(11, "Thickness", "System.Double");

      axis.Add(s1, 0.0);
      axis.Add(s2, 0.10);
      axis.Add(s3, 0.10);
      axis.Add(s4, 0.80);
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

      var pairs = axis.EnumerateAll()
                      .Select(t => $"{t.positionNormalized:F6}|{t.variableId}")
                      .OrderBy(s => s)
                      .ToArray();

      var dto = ProgesiAxisVariableDto.FromDomain(axis);
      var flat = dto.EnumerateFlat()
                    .Select(t => $"{t.Position:F6}|{t.VariableId}")
                    .OrderBy(s => s)
                    .ToArray();

      Assert.Equal(pairs, flat);
    }

    [Fact]
    public void ToDomain_ValidatesHeaders_AndEntries()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 1,
        AxisName = "AX",
        AxisLength = 5.0,
        Name = "V",
        ValueTypeKey = "System.Double",
        RuleId = 1,
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      // ok
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.0, VariableId = 1 });
      var ok = ProgesiAxisVariableDto.ToDomain(dto);
      Assert.Contains(1, ok.GetAt(0.0));

      // posizione fuori range (normalized) → eccezione
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 10.0, VariableId = 2 });
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
        Name = "V",
        ValueTypeKey = "System.Double",
        RuleId = null,
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      double tol = ProgesiAxisVariable.DefaultTolerance;
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.5, VariableId = 1 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.5 + tol * 0.4, VariableId = 2 });

      var axis = ProgesiAxisVariableDto.ToDomain(dto, tol);
      var at = axis.GetAt(0.5).OrderBy(x => x).ToArray();
      Assert.Equal(new[] { 1, 2 }, at);
    }

    [Fact]
    public void ToDomain_RejectsInvalidHeaders()
    {
      var good = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", Name = "V", ValueTypeKey = "System.Double" };
      Assert.NotNull(ProgesiAxisVariableDto.ToDomain(good));

      var bad1 = new ProgesiAxisVariableDto { AxisId = -1, AxisName = "AX", Name = "V", ValueTypeKey = "System.Double" };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad1));

      var bad2 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = null!, Name = "V", ValueTypeKey = "System.Double" };
      Assert.Throws<ArgumentNullException>(() => ProgesiAxisVariableDto.ToDomain(bad2));

      var badName = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", Name = null!, ValueTypeKey = "System.Double" };
      Assert.Throws<ArgumentNullException>(() => ProgesiAxisVariableDto.ToDomain(badName));

      var badType = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", Name = "V", ValueTypeKey = "  " };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(badType));

      var bad3 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", Name = "V", ValueTypeKey = "System.Double", AxisLength = 0.0 };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad3));

      var bad4 = new ProgesiAxisVariableDto { AxisId = 1, AxisName = "AX", Name = "V", ValueTypeKey = "System.Double", RuleId = -5 };
      Assert.Throws<ArgumentException>(() => ProgesiAxisVariableDto.ToDomain(bad4));
    }

    [Fact]
    public void ToDomain_RejectsInvalidEntries()
    {
      var dto = new ProgesiAxisVariableDto
      {
        AxisId = 1,
        AxisName = "AX",
        Name = "V",
        ValueTypeKey = "System.Double",
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = double.NaN, VariableId = 1 });
      Assert.Throws<ArgumentOutOfRangeException>(() => ProgesiAxisVariableDto.ToDomain(dto));

      dto.Entries.Clear();
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.0, VariableId = -1 });
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
        Name = "V",
        ValueTypeKey = "System.Double",
        Entries = new List<ProgesiAxisVariableDto.Entry>()
      };

      // Inseriamo in ordine “strano”
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.2, VariableId = 9 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.1, VariableId = 1 });
      dto.Entries.Add(new ProgesiAxisVariableDto.Entry { Position = 0.1, VariableId = 2 });

      var axis = ProgesiAxisVariableDto.ToDomain(dto);
      var dto2 = ProgesiAxisVariableDto.FromDomain(axis);

      Func<ProgesiAxisVariableDto.Entry, string> key = e => $"{e.Position:F6}|{e.VariableId}";
      var a = dto.Entries.Select(key).OrderBy(x => x).ToArray();
      var b = dto2.Entries.Select(key).OrderBy(x => x).ToArray();
      Assert.Equal(a, b);
    }
  }
}
