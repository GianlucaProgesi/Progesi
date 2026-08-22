using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using ProgesiRepositories.Rhino;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar.Presentation
{
  public sealed class AxisVarPresentationPipelineTests : IDisposable
  {
    private readonly RhinoDoc _doc;

    public AxisVarPresentationPipelineTests()
    {
      _doc = RhinoDocTestHelper.CreateTestDoc();
    }

    [Fact]
    public void BakeDiagram_TwoAxes_WritesExpectedTaggedAttributes()
    {
      var axes = new List<ProgesiAxisVariable>
      {
        CreateNumericAxis(11, "Top_Flange_thk", 30.0, 55.0),
        CreateNumericAxis(12, "Bottom_Flange_thk", 35.0, 65.0)
      };

      var settings = new DiagramSettings { ShowLegend = true, ShowTitles = false };
      var insertion = new Point3d(10, 20, 0);

      var result = AxisVarPresentationGhSupport.BakeDiagram(_doc, axes, settings, insertion);

      result.Block.BlockName.Should().StartWith("Progesi.AxisVar.Diagram");
      result.Scale.ScaleX.Should().BeGreaterThan(0);
      result.Scale.ScaleY.Should().BeGreaterThan(0);

      var instance = _doc.Objects.FindId(result.Block.InstanceId) as InstanceObject;
      instance.Should().NotBeNull();
      instance!.Attributes.GetUserString(PresentationBlockTags.BlockKind)
        .Should().Be(PresentationBlockTags.KindDiagram);
      instance.Attributes.GetUserString(PresentationBlockTags.ObjectIds).Should().Be("11,12");
      instance.Attributes.GetUserString(PresentationBlockTags.Mode).Should().Be("PlanXY");
      instance.Attributes.GetUserString(PresentationBlockTags.ScaleX).Should().NotBeNullOrEmpty();
      instance.Attributes.GetUserString(PresentationBlockTags.ScaleY).Should().NotBeNullOrEmpty();
      instance.Attributes.GetUserString(PresentationBlockTags.AnchorFrame).Should().Contain("10");
    }

    [Fact]
    public void BakeTable_TwoAxes_BuildsSharedStationRows_AndStringLabels()
    {
      RhinoTestBootstrap.Require();

      var numericAxis = CreateNumericAxis(21, "Top_Flange_thk", 30.0, 40.0);
      var stringAxis = new ProgesiAxisVariable(
        22,
        "Axis-B",
        "Material",
        "System.String",
        50.0,
        keyPoints: new[] { 0.0, 0.5, 1.0 });
      stringAxis.SetLabel(0.0, "Steel");
      stringAxis.SetLabel(0.5, "Aluminium");
      stringAxis.SetLabel(1.0, "Steel");
      AttachPlanLineCurve(stringAxis);

      var axes = new List<ProgesiAxisVariable> { numericAxis, stringAxis };
      var settings = new TableSettings();
      var insertion = new Point3d(0, 0, 0);

      var baked = AxisVarPresentationGhSupport.BakeTable(_doc, axes, settings, insertion, defaultUnit: "mm");

      baked.BlockName.Should().StartWith("Progesi.AxisVar.Table");

      var tableSeries = AxisVarPresentationGhSupport.BuildTableSeries(axes, settings, "mm");
      var layout = AxisTableLayoutEngine.Build(tableSeries, settings, new LayoutPoint2d(0, 0));

      layout.Cells.Any(c => c.Text == "Steel").Should().BeTrue();
      layout.Cells.Any(c => c.Text == "Aluminium").Should().BeTrue();
      layout.Cells.Any(c => c.Text == "30.00").Should().BeTrue();
      layout.RowCount.Should().BeGreaterThan(2);
      layout.ColumnCount.Should().Be(4);

      var instance = _doc.Objects.FindId(baked.InstanceId) as InstanceObject;
      instance.Should().NotBeNull();
      instance!.Attributes.GetUserString(PresentationBlockTags.BlockKind)
        .Should().Be(PresentationBlockTags.KindTable);
      instance.Attributes.GetUserString(PresentationBlockTags.ObjectIds).Should().Be("21,22");
    }

    private static ProgesiAxisVariable CreateNumericAxis(int id, string name, double startValue, double endValue)
    {
      var fn = new ProgesiFunction(id, name, new[]
      {
        new ProgesiFunctionSegment(0.0, 0.5, ProgesiFunctionSegmentKind.Constant, constantValue: startValue),
        new ProgesiFunctionSegment(0.5, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: endValue)
      });

      var axis = new ProgesiAxisVariable(
        id,
        "Axis-A",
        name,
        "System.Double",
        50.0,
        keyPoints: new[] { 0.0, 0.5, 1.0 },
        functionRef: ProgesiFunctionRef.Embed(fn));

      AttachPlanLineCurve(axis);
      return axis;
    }

    private static void AttachPlanLineCurve(ProgesiAxisVariable axis)
    {
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(axis.AxisLength ?? 50.0, 0, 0));
      axis.SetCurvePayload(ProgesiGeometryValueCodec.Encode(line));
      axis.SetMode(ProgesiCore.AxisCurveMode.PlanXY);
    }

    public void Dispose()
    {
      _doc?.Dispose();
    }
  }
}
