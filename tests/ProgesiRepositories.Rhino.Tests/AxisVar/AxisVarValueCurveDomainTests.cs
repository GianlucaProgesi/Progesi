using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarValueCurveDomainTests
  {
    private const double Tol = 1e-6;

    [Fact]
    public void DrawnValueCurve_RealX_NormalizedToAxisLength_InterpolateAtRealStations()
    {
      RhinoTestBootstrap.Require();

      const double axisLength = 10.0;
      var pts = new[]
      {
        new Point3d(0.0, 0.0, 0.0),
        new Point3d(2.5, 0.286, 0.0),
        new Point3d(5.0, 0.671, 0.0),
        new Point3d(7.5, 1.81, 0.0)
      };
      var drawn = NurbsCurve.Create(false, 3, pts);
      drawn.IsValid.Should().BeTrue();

      var normalizedDrawn = AxisVarGhSupport.NormalizeDrawnValueCurveToStationDomain(drawn, axisLength);
      var payload = ProgesiNurbsValueCurveCodec.FromCurve(normalizedDrawn);
      payload.ControlPoints.Select(cp => cp.X).Should().AllSatisfy(x => x.Should().BeInRange(0.0, 1.0));

      var fn = new ProgesiFunction(1, "vc", new[]
      {
        new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: payload)
      });
      var vc = new ProgesiValueCurve(fn);

      vc.Evaluate(2.5 / axisLength).Should().BeApproximately(0.286, Tol);
      vc.Evaluate(5.0 / axisLength).Should().BeApproximately(0.671, Tol);
      vc.Evaluate(7.5 / axisLength).Should().BeApproximately(1.81, Tol);
    }
  }
}
