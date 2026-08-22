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
    public void ByStationValue_Preserves_Duplicate_Real_Stations_In_Order()
    {
      RhinoTestBootstrap.Require();
      var stations = StationFactory.Create(new ByStationValueStrategy(new[] { 50.0, 50.0, 25.0 }), LineMapper());
      stations.Should().Equal(0.5, 0.5, 0.25);
    }

    [Fact]
    public void ByEqualSegments_N_Segments_Yields_N_Plus_One_Stations()
    {
      RhinoTestBootstrap.Require();
      const int n = 10;
      var mapper = LineMapper();
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(n), mapper);
      stations.Should().HaveCount(n + 1);
      stations.First().Should().Be(0.0);
      stations.Last().Should().Be(1.0);
      for (int i = 0; i <= n; i++)
        stations[i].Should().BeApproximately(i / (double)n, 1e-12);
    }

    [Fact]
    public void ByEqualSegments_Five_Segments_Yields_Six_Stations()
    {
      RhinoTestBootstrap.Require();
      var stations = StationFactory.Create(new ByEqualSegmentsStrategy(5), LineMapper());
      stations.Should().HaveCount(6);
      stations.Should().Equal(0.0, 0.2, 0.4, 0.6, 0.8, 1.0);
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
