using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.Rhino;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests
{
  public sealed class ProgesiNurbsValueCurveCodecTests
  {
    [Fact]
    public void Payload_To_NurbsCurve_To_Payload_RoundTrips()
    {
      RhinoTestBootstrap.Require();
      var payload = new ProgesiNurbsPayload(
        1,
        new[] { (0.0, 1.0), (5.0, 3.0), (10.0, 2.0) },
        new[] { 1.0, 1.0, 1.0 },
        new[] { 0.0, 0.0, 0.5, 1.0, 1.0 });

      var curve = ProgesiNurbsValueCurveCodec.ToNurbsCurve(payload);
      var roundTrip = ProgesiNurbsValueCurveCodec.FromCurve(curve);

      roundTrip.Degree.Should().Be(payload.Degree);
      roundTrip.ControlPoints.Select(cp => cp.X).Should().Equal(payload.ControlPoints.Select(cp => cp.X));
      roundTrip.ControlPoints.Select(cp => cp.Value).Should().Equal(payload.ControlPoints.Select(cp => cp.Value));
    }

    [Fact]
    public void Drawn_Curve_Degree3_FourControlPoints_RoundTrips_Standard_Knots()
    {
      RhinoTestBootstrap.Require();

      var pts = new[]
      {
        new Point3d(0.0, 1.0, 0.0),
        new Point3d(1.0, 2.0, 0.0),
        new Point3d(2.0, 1.5, 0.0),
        new Point3d(3.0, 3.0, 0.0)
      };
      var drawn = NurbsCurve.Create(false, 3, pts);
      drawn.IsValid.Should().BeTrue();

      var payload = ProgesiNurbsValueCurveCodec.FromCurve(drawn);
      payload.Degree.Should().Be(3);
      payload.ControlPoints.Should().HaveCount(4);
      payload.Knots.Should().HaveCount(8, "standard knot vector is cp + degree + 1");

      var roundTripCurve = ProgesiNurbsValueCurveCodec.ToNurbsCurve(payload);
      roundTripCurve.IsValid.Should().BeTrue();

      var roundTripPayload = ProgesiNurbsValueCurveCodec.FromCurve(roundTripCurve);
      roundTripPayload.Degree.Should().Be(payload.Degree);
      roundTripPayload.Knots.Should().Equal(payload.Knots);
      roundTripPayload.ControlPoints.Select(cp => cp.X).Should().Equal(payload.ControlPoints.Select(cp => cp.X));
      roundTripPayload.ControlPoints.Select(cp => cp.Value).Should().Equal(payload.ControlPoints.Select(cp => cp.Value));
    }
  }
}
