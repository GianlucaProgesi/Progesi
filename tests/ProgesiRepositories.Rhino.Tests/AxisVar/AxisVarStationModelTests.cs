using System;
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
  public sealed class AxisVarStationModelTests : IDisposable
  {
    private RhinoDoc? _doc;
    private RhinoAxisVariableRepository? _axisRepo;
    private RhinoVariableRepository? _varRepo;

    private void RequireRhinoStores()
    {
      RhinoTestBootstrap.Require();
      _doc ??= RhinoDocTestHelper.CreateTestDoc();
      _axisRepo ??= new RhinoAxisVariableRepository(_doc);
      _varRepo ??= new RhinoVariableRepository(_doc);
    }

    public void Dispose() => _doc?.Dispose();

    private static CurveParameterMapper LineMapper(double length = 100.0)
    {
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(length, 0, 0));
      return new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
    }

    private static ProgesiAxisVariable MakeAxis(string curvePayload, double axisLength = 100.0)
    {
      return new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Thickness",
        "System.Double",
        axisLength,
        curvePayload: curvePayload,
        mode: ProgesiCore.AxisCurveMode.Curve3d,
        keyPoints: Array.Empty<double>());
    }

    [Fact]
    public void MergeKeyPoints_Additive_Union_With_Tolerance_Dedup()
    {
      RhinoTestBootstrap.Require();
      var eqStations = StationFactory.Create(new ByEqualSegmentsStrategy(10), LineMapper());
      eqStations.Should().HaveCount(11);

      var extra = new[] { 0.05, 0.15, 0.95, 1.0 };
      var merged = AxisVarGhSupport.MergeKeyPoints(eqStations, extra);

      merged.Should().HaveCount(14);
      merged.First().Should().Be(0.0);
      merged.Last().Should().Be(1.0);
    }

    [Fact]
    public void ByStationValue_Preserves_Input_Order_And_Duplicates()
    {
      RhinoTestBootstrap.Require();

      var stations = StationFactory.Create(
        new ByStationValueStrategy(new[] { 30.0, 10.0, 30.0 }),
        LineMapper());

      stations.Should().Equal(0.3, 0.1, 0.3);
    }

    [Fact]
    public void AssignSidesForStations_Discontinuity_Left_Right_Pairs()
    {
      var stations = new List<double>();
      for (int i = 0; i < 21; i++)
        stations.Add(i / 20.0);
      // Add discontinuity pairs at positions not already on the uniform grid.
      stations.Add(0.12);
      stations.Add(0.12);
      stations.Add(0.87);

      var sides = AxisVarGhSupport.AssignSidesForStations(stations);
      sides.Should().HaveCount(24);

      int idx12First = stations.FindIndex(s => Math.Abs(s - 0.12) < 1e-9);
      sides[idx12First].Should().Be(ProgesiAxisStationSide.Left);
      sides[idx12First + 1].Should().Be(ProgesiAxisStationSide.Right);
      sides[stations.Count - 1].Should().Be(ProgesiAxisStationSide.None);
    }

    [Fact]
    public void OptionB_Values_Create_And_Dedup_Real_ProgesiVariables()
    {
      RequireRhinoStores();

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
      var payload = ProgesiGeometryValueCodec.Encode(line);
      var axis = MakeAxis(payload);
      var edited = AxisVarGhSupport.CloneForEdit(axis);

      var stations = new[] { 0.0, 0.5, 1.0 };
      var values = new object[] { 10.0, 20.0, 10.0 };
      var sides = AxisVarGhSupport.AssignSidesForStations(stations);

      var ids = values
        .Select(v => AxisVarGhSupport.ResolveOrCreateVariable(
          _varRepo!, axis.Name, axis.ValueTypeKey,
          AxisVarGhSupport.CoerceTypedValue(v, axis.ValueTypeKey)))
        .ToList();

      ids[0].Should().Be(ids[2], "identical values dedup by ContentHash");
      ids.Distinct().Should().HaveCount(2);

      AxisVarGhSupport.ApplyKeyPointsAndOptionalVariables(edited, stations, ids, sides);

      var saved = _axisRepo!.SaveAsync(edited).GetAwaiter().GetResult();
      var entries = saved.EnumerateAll().OrderBy(e => e.positionNormalized).ToList();
      entries.Should().HaveCount(3);
      entries.Select(e => e.variableId).Should().Equal(ids);

      var resolved = entries
        .Select(e => AxisVarGhSupport.ResolveVariableValue(_varRepo!, e.variableId))
        .ToList();
      resolved.Should().Equal(10.0, 20.0, 10.0);
    }

    [Fact]
    public void ReplaceMode_Clears_Previous_Stations()
    {
      var axis = new ProgesiAxisVariable(
        2,
        "Axis-B",
        "Material",
        "System.String",
        50.0,
        keyPoints: new[] { 0.0, 0.25, 0.5, 0.75, 1.0 });
      axis.SetLabel(0.25, "old");

      var edited = AxisVarGhSupport.CloneForEdit(axis);
      var newStations = new[] { 0.0, 1.0 };
      AxisVarGhSupport.ApplyKeyPointsAndOptionalVariables(edited, newStations, replace: true);

      edited.KeyPoints.Should().Equal(0.0, 1.0);
      edited.GetLabels().Should().BeEmpty();
      edited.EnumerateAll().Should().BeEmpty();
    }

    [Fact]
    public void Labels_RoundTrip_Separately_From_Values()
    {
      RequireRhinoStores();

      var axis = MakeAxis(ProgesiGeometryValueCodec.Encode(
        new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 0))));
      var edited = AxisVarGhSupport.CloneForEdit(axis);
      var stations = new[] { 0.0, 0.5, 1.0 };
      var ids = new List<int> { 1, 2, 3 };
      for (int i = 0; i < ids.Count; i++)
      {
        _varRepo!.SaveAsync(new ProgesiVariable(ids[i], axis.Name, (double)(i + 1) * 10.0))
          .GetAwaiter().GetResult();
      }

      AxisVarGhSupport.ApplyKeyPointsAndOptionalVariables(edited, stations, ids);
      AxisVarGhSupport.ApplyOptionalStationLabels(edited, stations, new[] { "Start", "Mid", "End" });

      edited.GetLabel(0.5).Should().Be("Mid");
      AxisVarGhSupport.ResolveVariableValue(_varRepo!, ids[1]).Should().Be(20.0);
    }

    [Fact]
    public void ModeOverride_RealStations_Differ_Between_Curve3d_And_PlanXY()
    {
      RhinoTestBootstrap.Require();

      var curve = new PolylineCurve(new[]
      {
        new Point3d(0, 0, 0),
        new Point3d(3, 4, 0),
        new Point3d(10, 4, 0)
      });

      var mapper3d = new CurveParameterMapper(curve, ProgesiCore.AxisCurveMode.Curve3d);
      var mapperPlan = new CurveParameterMapper(curve, ProgesiCore.AxisCurveMode.PlanXY);

      mapper3d.TotalLength.Should().NotBeApproximately(mapperPlan.TotalLength, 1e-6);
      mapper3d.NormalizedToReal(0.5).Should().NotBeApproximately(mapperPlan.NormalizedToReal(0.5), 1e-6);
    }
  }
}
