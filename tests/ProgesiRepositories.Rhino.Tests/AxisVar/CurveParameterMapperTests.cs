using System;
using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class CurveParameterMapperTests
  {
    private const double Tol = 1e-4;

    [Fact]
    public void StraightLine_Curve3d_RoundTrips_Exactly()
    {
      RhinoTestBootstrap.Require();
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);

      mapper.TotalLength.Should().BeApproximately(10.0, Tol);
      mapper.NormalizedToReal(0.5).Should().BeApproximately(5.0, Tol);
      mapper.RealToNormalized(5.0).Should().BeApproximately(0.5, Tol);

      mapper.TryRealToParameter(5.0, out double t).Should().BeTrue();
      mapper.TryParameterToReal(t, out double real).Should().BeTrue();
      real.Should().BeApproximately(5.0, Tol);
      mapper.ParameterToPoint3d(t).DistanceTo(new Point3d(5, 0, 0)).Should().BeLessThan(Tol);
    }

    [Fact]
    public void CurvedNurbs_Curve3d_ArcLength_Is_Monotonic()
    {
      RhinoTestBootstrap.Require();
      var pts = new[]
      {
        new Point3d(0, 0, 0),
        new Point3d(5, 5, 0),
        new Point3d(10, 0, 0)
      };
      var curve = Curve.CreateInterpolatedCurve(pts, 3);
      var mapper = new CurveParameterMapper(curve, ProgesiCore.AxisCurveMode.Curve3d);

      double prev = -1.0;
      foreach (var n in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
      {
        mapper.TryNormalizedToParameter(n, out double t).Should().BeTrue();
        mapper.TryParameterToReal(t, out double real).Should().BeTrue();
        real.Should().BeGreaterThan(prev);
        prev = real;
      }
    }

    [Fact]
    public void PlanXY_Uses_Projected_ArcLength()
    {
      RhinoTestBootstrap.Require();
      var line = new LineCurve(new Point3d(0, 0, 5), new Point3d(3, 4, 5));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.PlanXY);

      mapper.TotalLength.Should().BeApproximately(5.0, Tol);
      mapper.TryNormalizedToPoint3d(1.0, out var end).Should().BeTrue();
      end.Z.Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Profile_Abscissa_Is_Plan_ArcLength_Elevation_From_Z()
    {
      RhinoTestBootstrap.Require();
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 20));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Profile);

      mapper.TotalLength.Should().BeApproximately(10.0, Tol);
      mapper.TryNormalizedToParameter(0.5, out double t).Should().BeTrue();
      mapper.GetElevationAtParameter(t).Should().BeApproximately(10.0, Tol);
    }

    [Fact]
    public void ProjectionService_BuildProfileCurve_Has_Station_And_Elevation()
    {
      RhinoTestBootstrap.Require();
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(10, 0, 20));
      var profile = ProjectionService.BuildProfileCurve(line);
      profile.GetLength().Should().BeApproximately(10.0, 0.5);
      profile.PointAtStart.Y.Should().BeApproximately(0.0, Tol);
      profile.PointAtEnd.Y.Should().BeApproximately(20.0, Tol);
    }
  }
}
