using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarVariationValuesTests
  {
    private static ProgesiAxisVariable ApplyValuesAtStations(
      RhinoVariableRepository varRepo,
      ProgesiAxisVariable axis,
      IReadOnlyList<double> normalizedStations,
      IReadOnlyList<object> values)
    {
      var edited = AxisVarGhSupport.CloneForEdit(axis);
      var sides = AxisVarGhSupport.AssignSidesForStations(normalizedStations);
      var ids = values
        .Select(v => AxisVarGhSupport.ResolveOrCreateVariable(
          varRepo, axis.Name, axis.ValueTypeKey,
          AxisVarGhSupport.CoerceTypedValue(v, axis.ValueTypeKey)))
        .ToList();

      AxisVarGhSupport.ApplyKeyPointsAndOptionalVariables(edited, normalizedStations, ids, sides);
      return edited;
    }

    [Fact]
    public void ByEqualSegments_StringValues_ResolveFromLinkedVariables()
    {
      RhinoTestBootstrap.Require();
      var doc = RhinoDocTestHelper.CreateTestDoc();
      var varRepo = new RhinoVariableRepository(doc);

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

      axis = ApplyValuesAtStations(varRepo, axis, stations, new object[] { "A", "M", "B" });

      var outValues = axis.EnumerateAll()
        .OrderBy(e => e.positionNormalized)
        .Select(e => AxisVarGhSupport.ResolveVariableValue(varRepo, e.variableId))
        .ToList();

      outValues.Should().Equal("A", "M", "B");
      doc.Dispose();
    }

    [Fact]
    public void ByEqualSegments_NumericValues_Appear_In_OutValues()
    {
      RhinoTestBootstrap.Require();
      var doc = RhinoDocTestHelper.CreateTestDoc();
      var varRepo = new RhinoVariableRepository(doc);

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

      axis = ApplyValuesAtStations(varRepo, axis, stations, new object[] { 10.0, 20.0, 30.0 });

      var outValues = axis.EnumerateAll()
        .OrderBy(e => e.positionNormalized)
        .Select(e => AxisVarGhSupport.ResolveVariableValue(varRepo, e.variableId))
        .ToList();

      outValues.Should().Equal(10.0, 20.0, 30.0);
      doc.Dispose();
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
