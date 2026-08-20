using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarVariationValuesTests
  {
    private static ProgesiAxisVariable ApplyLabelsAtStations(
      ProgesiAxisVariable axis,
      IReadOnlyList<double> normalizedStations,
      IReadOnlyList<object> values)
    {
      var edited = axis;
      edited.SetKeyPoints(normalizedStations);
      for (int i = 0; i < normalizedStations.Count; i++)
        edited.SetLabel(normalizedStations[i], AxisVarGhSupport.CoerceValueLabel(values[i]));
      return edited;
    }

    [Fact]
    public void ByEqualSegments_StringValues_InterpolateAndOut_Align()
    {
      RhinoTestBootstrap.Require();

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(2), mapper);

      stations.Should().Equal(0.0, 0.5, 1.0);

      var axis = new ProgesiAxisVariable(
        11,
        "Axis-A",
        "Material",
        "System.String",
        10.0,
        keyPoints: stations);

      axis = ApplyLabelsAtStations(axis, stations, new object[] { "A", "M", "B" });

      AxisVarGhSupport.EvaluateStepValue(axis, 0.4).Should().Be("A");

      var outValues = stations.Select(n => axis.GetLabel(n) ?? string.Empty).ToList();
      outValues.Should().Equal("A", "M", "B");
    }

    [Fact]
    public void ByEqualSegments_NumericValues_Appear_In_OutValues()
    {
      RhinoTestBootstrap.Require();

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(2), mapper);

      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Thickness",
        "System.Double",
        100.0,
        keyPoints: stations);

      axis = ApplyLabelsAtStations(axis, stations, new object[] { 10.0, 20.0, 30.0 });

      var outValues = stations.Select(n => axis.GetLabel(n) ?? string.Empty).ToList();
      outValues.Should().Equal("10", "20", "30");
    }

    [Fact]
    public void ValuesCountMismatch_Error_Includes_Expected_Station_Count()
    {
      RhinoTestBootstrap.Require();

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(2), mapper);

      System.Action act = () =>
      {
        if (2 != stations.Count)
          throw new System.InvalidOperationException(
            $"Values count (2) must match station count ({stations.Count}).");
      };

      act.Should().Throw<System.InvalidOperationException>()
        .WithMessage("*Values count (2) must match station count (3)*");
    }
  }
}
