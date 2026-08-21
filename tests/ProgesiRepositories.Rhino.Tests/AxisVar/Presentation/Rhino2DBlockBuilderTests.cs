using System;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Xunit;
using CurveParameterMapper = ProgesiGrasshopperAssembly.Infrastructure.AxisVar.CurveParameterMapper;

namespace ProgesiRepositories.Rhino.Tests.AxisVar.Presentation
{
  public sealed class Rhino2DBlockBuilderTests : IDisposable
  {
    private readonly RhinoDoc _doc;

    public Rhino2DBlockBuilderTests()
    {
      _doc = RhinoDocTestHelper.CreateTestDoc();
    }

    [Fact]
    public void BakeDiagram_WritesTaggedAttributes_OnInstance()
    {
      const double length = 50.0;
      var fn = new ProgesiFunction(1, "law", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 40.0)
      });

      var axis = new ProgesiAxisVariable(
        7,
        "Axis-A",
        "Thickness",
        "System.Double",
        length,
        keyPoints: new[] { 0.0, 1.0 },
        functionRef: ProgesiFunctionRef.Embed(fn));

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(length, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.PlanXY);
      var series = new System.Collections.Generic.List<AxisDiagramSeries>
      {
        new AxisDiagramSeries(axis, mapper, unchecked((int)0xFF0072B2), "Thickness")
      };

      var settings = new DiagramSettings { ShowLegend = false, ShowTitles = false };
      var (min, max) = AxisDiagramLayoutEngine.ComputeOrdinateRange(series);
      var scale = ScaleFactorOptimiser.Optimise(length, min, max, settings);
      var layout = AxisDiagramLayoutEngine.Build(series, scale, settings);

      var insertion = new Point3d(100, 200, 0);
      var result = Rhino2DBlockBuilder.BakeDiagram(_doc, layout, scale, insertion, "Progesi.AxisVar.TestDiagram");

      result.BlockName.Should().StartWith("Progesi.AxisVar.TestDiagram");
      _doc.InstanceDefinitions.Find(result.BlockName).Should().NotBeNull();

      var instance = _doc.Objects.FindId(result.InstanceId) as InstanceObject;
      instance.Should().NotBeNull();

      instance!.Attributes.GetUserString(PresentationBlockTags.BlockKind)
        .Should().Be(PresentationBlockTags.KindDiagram);
      instance.Attributes.GetUserString(PresentationBlockTags.ObjectIds).Should().Be("7");
      instance.Attributes.GetUserString(PresentationBlockTags.Mode).Should().Be("PlanXY");
      instance.Attributes.GetUserString(PresentationBlockTags.ScaleX).Should().NotBeNullOrEmpty();
      instance.Attributes.GetUserString(PresentationBlockTags.ScaleY).Should().NotBeNullOrEmpty();
      instance.Attributes.GetUserString(PresentationBlockTags.AnchorFrame).Should().Contain("100");
    }

    public void Dispose()
    {
      _doc?.Dispose();
    }
  }
}
