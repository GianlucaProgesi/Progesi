using System;
using System.Linq;
using Xunit;
using ProgesiCore;

namespace ProgesiCore.Tests
{
  public class ProgesiAxisVariableTests
  {
    private static ProgesiAxisVariable Make(
      int id = 1,
      string axisName = "Axis-1",
      string name = "Thickness",
      string valueTypeKey = "System.Double",
      double? axisLength = null,
      int? ruleId = null)
        => new ProgesiAxisVariable(id, axisName, name, valueTypeKey, axisLength, ruleId);

    [Fact]
    public void Ctor_Throws_OnNegativeId()
    {
      Assert.Throws<ArgumentException>(() => Make(id: -1));
    }

    [Fact]
    public void Ctor_Throws_OnNullAxisName()
    {
      Assert.Throws<ArgumentNullException>(() => new ProgesiAxisVariable(1, null!, "N", "T"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_OnEmptyOrWhiteSpaceAxisName(string axisName)
    {
      Assert.Throws<ArgumentException>(() => new ProgesiAxisVariable(1, axisName, "N", "T"));
    }

    [Fact]
    public void Ctor_Throws_OnNullSeriesName()
    {
      Assert.Throws<ArgumentNullException>(() => new ProgesiAxisVariable(1, "AX", null!, "T"));
    }

    [Fact]
    public void Ctor_Throws_OnNullValueTypeKey()
    {
      Assert.Throws<ArgumentNullException>(() => new ProgesiAxisVariable(1, "AX", "N", null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Ctor_Throws_OnNonPositiveAxisLength(double len)
    {
      Assert.Throws<ArgumentException>(() => Make(axisLength: len));
    }

    [Fact]
    public void Ctor_SetsProperties_Trimmed()
    {
      var sut = new ProgesiAxisVariable(10, " Gr-1 ", " Thickness ", " System.Double ", 100.0, 99);
      Assert.Equal(10, sut.Id);
      Assert.Equal("Gr-1", sut.AxisName);
      Assert.Equal("Thickness", sut.Name);
      Assert.Equal("System.Double", sut.ValueTypeKey);
      Assert.Equal(100.0, sut.AxisLength);
      Assert.Equal(99, sut.RuleId);
    }

    [Fact]
    public void Add_StoresOnlyIds_AndAvoidsDuplicates()
    {
      var sut = Make();
      var sig = new ProgesiAxisVariable.ProgesiVariableSignature(100, "Thickness", "System.Double");
      sut.Add(sig, 0.25);
      sut.Add(sig, 0.25);
      var at = sut.GetAt(0.25);
      Assert.Single(at);
      Assert.Equal(100, at.First());
    }

    [Fact]
    public void Add_AllowsMultipleIdsSamePosition()
    {
      var sut = Make();
      sut.Add(new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double"), 0.5);
      sut.Add(new ProgesiAxisVariable.ProgesiVariableSignature(3, "Thickness", "System.Double"), 0.5);
      var at = sut.GetAt(0.5).OrderBy(x => x).ToArray();
      Assert.Equal(new[] { 1, 3 }, at);
    }

    [Fact]
    public void Add_Throws_OnNameMismatch()
    {
      var sut = Make(name: "A");
      var bad = new ProgesiAxisVariable.ProgesiVariableSignature(1, "B", "System.Double");
      Assert.Throws<InvalidOperationException>(() => sut.Add(bad, 0.1));
    }

    [Fact]
    public void Add_Throws_OnValueTypeMismatch()
    {
      var sut = Make(valueTypeKey: "System.Double");
      var bad = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Int32");
      Assert.Throws<InvalidOperationException>(() => sut.Add(bad, 0.1));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Positions_AreNormalized_AndValidated(double s)
    {
      var sut = Make();
      var sig = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double");
      Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add(sig, s));
    }

    [Fact]
    public void Rebucket_ByTolerance_GroupsCloseStations()
    {
      var sut = Make();
      var sig1 = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double");
      var sig2 = new ProgesiAxisVariable.ProgesiVariableSignature(2, "Thickness", "System.Double");
      double tol = ProgesiAxisVariable.DefaultTolerance;
      sut.Add(sig1, 0.5, tol);
      sut.Add(sig2, 0.5 + tol * 0.4, tol);
      var at = sut.GetAt(0.5, tol).OrderBy(x => x).ToArray();
      Assert.Equal(new[] { 1, 2 }, at);
    }

    [Fact]
    public void Real_Normalized_Conversion_RequiresAxisLength()
    {
      var sut = Make(axisLength: null);
      Assert.Throws<InvalidOperationException>(() => sut.ToNormalizedFromReal(1.0));
      Assert.Throws<InvalidOperationException>(() => sut.ToRealFromNormalized(0.5));
    }

    [Fact]
    public void Real_Normalized_Conversion_Works()
    {
      var sut = Make(axisLength: 10.0);
      Assert.Equal(0.2, sut.ToNormalizedFromReal(2.0), 12);
      Assert.Equal(2.0, sut.ToRealFromNormalized(0.2), 12);
    }

    [Fact]
    public void Move_And_Remove_Work()
    {
      var sut = Make();
      var sig = new ProgesiAxisVariable.ProgesiVariableSignature(1, "Thickness", "System.Double");
      sut.Add(sig, 0.1);
      Assert.True(sut.Move(0.1, 0.2, 1));
      Assert.Empty(sut.GetAt(0.1));
      Assert.Contains(1, sut.GetAt(0.2));

      Assert.True(sut.RemoveAt(0.2, 1));
      Assert.Empty(sut.GetAt(0.2));
    }
  }
}
