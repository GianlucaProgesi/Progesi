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
  }
}
