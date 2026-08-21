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
  public sealed class AxisTableLayoutEngineTests
  {
    [Fact]
    public void Build_StringValues_PassThrough_AsLabels()
    {
      RhinoTestBootstrap.Require();

      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Material",
        "System.String",
        10.0,
        keyPoints: new[] { 0.0, 1.0 });
      axis.SetLabel(0.0, "A");
      axis.SetLabel(1.0, "B");

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);

      var series = new System.Collections.Generic.List<AxisTableLayoutEngine.TableSeries>
      {
        new AxisTableLayoutEngine.TableSeries(axis, mapper, "Material", string.Empty)
      };

      var layout = AxisTableLayoutEngine.Build(series, new TableSettings(), new LayoutPoint2d(0, 0));

      layout.RowCount.Should().Be(3);
      layout.Cells.Any(c => c.Text == "A").Should().BeTrue();
      layout.Cells.Any(c => c.Text == "B").Should().BeTrue();
    }
  }
}
