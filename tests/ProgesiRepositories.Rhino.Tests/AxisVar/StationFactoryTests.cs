using System.Linq;
using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class StationFactoryTests
  {
    private static CurveParameterMapper LineMapper()
    {
      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
      return new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
    }

    [Fact]
    public void ByStationValue_Converts_Real_To_Normalized()
    {
      RhinoTestBootstrap.Require();
      var stations = StationFactory.Create(new ByStationValueStrategy(new[] { 0.0, 25.0, 100.0 }), LineMapper());
      stations.Should().Equal(0.0, 0.25, 1.0);
    }

    [Fact]
    public void ByEqualSegments_Divides_Into_N_Parts()
    {
      RhinoTestBootstrap.Require();
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(5), LineMapper());
      stations.Should().HaveCount(5);
      stations.First().Should().Be(0.0);
      stations.Last().Should().Be(1.0);
    }

    [Fact]
    public void BySegmentLength_Spaces_By_Real_Length()
    {
      RhinoTestBootstrap.Require();
      var stations = StationFactory.Create(new BySegmentLengthStrategy(25.0), LineMapper());
      stations.Should().Contain(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 });
    }

    [Fact]
    public void ByPoints_Projects_To_Nearest_Station()
    {
      RhinoTestBootstrap.Require();
      var mapper = LineMapper();
      var stations = StationFactory.Create(new ByPointsStrategy(new[] { new Point3d(50, 2, 0), new Point3d(10, 0, 0) }), mapper);
      stations.Should().Contain(0.1);
      stations.Should().Contain(0.5);
    }

    [Fact]
    public void InheritFrom_Reuses_Normalized_Stations()
    {
      RhinoTestBootstrap.Require();
      var inherited = new[] { 0.0, 0.33, 0.66, 1.0 };
      var stations = StationFactory.Create(new InheritFromStrategy(inherited), LineMapper());
      stations.Should().Equal(inherited);
    }
  }
}
