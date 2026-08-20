using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarLabelValueTests
  {
    [Fact]
    public void CoerceValueLabel_Preserves_String_And_Formats_Numeric()
    {
      AxisVarGhSupport.CoerceValueLabel("A").Should().Be("A");
      AxisVarGhSupport.CoerceValueLabel(1.25).Should().Be("1.25");
    }

    [Fact]
    public void ByStationValue_StringLabels_Interpolate_Returns_Nearest_Keypoint()
    {
      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Material",
        "System.String",
        10.0,
        keyPoints: new[] { 0.0, 1.0 });

      axis.SetLabel(0.0, "A");
      axis.SetLabel(1.0, "B");

      AxisVarGhSupport.EvaluateStepValue(axis, 0.4).Should().Be("A");
    }

    [Fact]
    public void OutValues_Align_With_Keypoint_Labels()
    {
      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Thickness",
        "System.Double",
        10.0,
        keyPoints: new[] { 0.0, 0.5, 1.0 });

      axis.SetLabel(0.0, "10");
      axis.SetLabel(0.5, "20");
      axis.SetLabel(1.0, "30");

      var normalized = axis.KeyPoints.ToList();
      var values = normalized.Select(n => axis.GetLabel(n) ?? string.Empty).ToList();

      values.Should().Equal("10", "20", "30");
    }
  }
}
