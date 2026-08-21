using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;
using CurveParameterMapper = ProgesiGrasshopperAssembly.Infrastructure.AxisVar.CurveParameterMapper;

namespace ProgesiRepositories.Rhino.Tests.AxisVar.Presentation
{
  public sealed class AxisDiagramLayoutEngineTests
  {
    private const double SsotLength = 98.968639;

    [Fact]
    public void Build_SsotRanges_ProducesPlotTicksPolylineAndDiscontinuity()
    {
      RhinoTestBootstrap.Require();

      var series = CreateSsotSeriesWithStep();
      var settings = new DiagramSettings { GridEnabled = false, ShowLegend = false, ShowTitles = false };
      var (min, max) = AxisDiagramLayoutEngine.ComputeOrdinateRange(series);
      min.Should().BeApproximately(30.0, 1e-6);
      max.Should().BeApproximately(65.0, 1e-6);

      var scale = ScaleFactorOptimiser.Optimise(SsotLength, min, max, settings);
      var layout = AxisDiagramLayoutEngine.Build(series, scale, settings);

      layout.PlotWidth.Should().BeApproximately(scale.PlotWidth, 1e-6);
      layout.PlotHeight.Should().BeApproximately(scale.PlotHeight, 1e-6);
      layout.ObjectIdsCsv.Should().Be("1");

      layout.Lines.Count(l => !l.IsGrid).Should().BeGreaterThan(4);
      layout.Polylines.Should().HaveCount(1);
      layout.Polylines[0].Points.Count.Should().BeGreaterThan(4);

      layout.Texts.Count(t => t.IsTickLabel).Should().BeGreaterThan(4);

      layout.Lines.Any(l =>
        !l.IsGrid &&
        System.Math.Abs(l.Start.X - l.End.X) < 1e-6 &&
        System.Math.Abs(l.Start.Y - l.End.Y) > 1.0).Should().BeTrue("discontinuity vertical segment");
    }

    private static System.Collections.Generic.List<AxisDiagramSeries> CreateSsotSeriesWithStep()
    {
      var fn = new ProgesiFunction(1, "step", new[]
      {
        new ProgesiFunctionSegment(0.0, 0.5, ProgesiFunctionSegmentKind.Constant, constantValue: 30.0),
        new ProgesiFunctionSegment(0.5, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: 65.0)
      });

      var axis = new ProgesiAxisVariable(
        1,
        "Bridge-A",
        "Thickness",
        "System.Double",
        SsotLength,
        keyPoints: new[] { 0.0, 0.5, 1.0 },
        functionRef: ProgesiFunctionRef.Embed(fn));

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(SsotLength, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);

      return new System.Collections.Generic.List<AxisDiagramSeries>
      {
        new AxisDiagramSeries(axis, mapper, unchecked((int)0xFF0072B2), "Thickness")
      };
    }
  }
}
