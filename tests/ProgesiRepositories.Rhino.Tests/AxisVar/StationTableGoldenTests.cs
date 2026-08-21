using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino.Tests.Support;
using Rhino.Geometry;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class StationTableGoldenTests
  {
    private static string RepoRoot()
    {
      var dir = AppContext.BaseDirectory;
      while (dir != null && !Directory.Exists(Path.Combine(dir, "validation", "axisvar", "golden")))
        dir = Directory.GetParent(dir)?.FullName;
      return dir ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    [Fact]
    public void Golden_3D_vs_Projected_Station_Table_Matches_Expected()
    {
      RhinoTestBootstrap.Require();

      var goldenPath = Path.Combine(RepoRoot(), "validation", "axisvar", "golden", "station-table-3d-vs-projected.json");
      File.Exists(goldenPath).Should().BeTrue("golden fixture must exist at validation/axisvar/golden/station-table-3d-vs-projected.json");

      var json = JObject.Parse(File.ReadAllText(goldenPath));
      double tol = json.Value<double>("tolerance");
      var start = json["curve"]!["start"]!.ToObject<double[]>()!;
      var end = json["curve"]!["end"]!.ToObject<double[]>()!;
      var realStations = json["realStations"]!.ToObject<double[]>()!;
      var line = new LineCurve(new Point3d(start[0], start[1], start[2]), new Point3d(end[0], end[1], end[2]));

      foreach (var modeCase in json["modes"]!)
      {
        var modeName = modeCase.Value<string>("mode")!;
        ProgesiCore.AxisCurveMode mode;
        switch (modeName)
        {
          case "Curve3d": mode = ProgesiCore.AxisCurveMode.Curve3d; break;
          case "PlanXY": mode = ProgesiCore.AxisCurveMode.PlanXY; break;
          case "Profile": mode = ProgesiCore.AxisCurveMode.Profile; break;
          default: throw new ArgumentOutOfRangeException(nameof(modeName), modeName, "Unknown mode in golden file.");
        }
        var mapper = new CurveParameterMapper(line, mode);

        mapper.TotalLength.Should().BeApproximately(modeCase.Value<double>("totalLength"), tol,
          $"{modeName} total length");

        var expectedNorm = modeCase["expectedNormalized"]!.ToObject<double[]>()!;
        var actualNorm = realStations.Select(s => mapper.RealToNormalized(s)).ToArray();
        actualNorm.Should().HaveSameCount(expectedNorm);
        for (int i = 0; i < expectedNorm.Length; i++)
          actualNorm[i].Should().BeApproximately(expectedNorm[i], tol, $"{modeName} station {i}");

        if (modeCase["expectedElevationsAtNormalized"] != null)
        {
          var expectedElev = modeCase["expectedElevationsAtNormalized"]!.ToObject<double[]>()!;
          for (int i = 0; i < expectedNorm.Length; i++)
          {
            mapper.TryNormalizedToParameter(expectedNorm[i], out double t).Should().BeTrue();
            mapper.GetElevationAtParameter(t).Should().BeApproximately(expectedElev[i], tol);
          }
        }
      }
    }

    [Fact]
    public void Golden_EqualSegments_N10_Yields_N_Plus_One_Stations()
    {
      RhinoTestBootstrap.Require();

      var goldenPath = Path.Combine(RepoRoot(), "validation", "axisvar", "golden", "equal-segments-n10.json");
      File.Exists(goldenPath).Should().BeTrue("golden fixture must exist at validation/axisvar/golden/equal-segments-n10.json");

      var json = JObject.Parse(File.ReadAllText(goldenPath));
      double tol = json.Value<double>("tolerance");
      int n = json.Value<int>("segmentCount");
      double axisLength = json.Value<double>("axisLength");
      var expectedNorm = json["expectedNormalized"]!.ToObject<double[]>()!;
      var expectedReal = json["expectedReal"]!.ToObject<double[]>()!;

      var line = new LineCurve(new Point3d(0, 0, 0), new Point3d(axisLength, 0, 0));
      var mapper = new CurveParameterMapper(line, ProgesiCore.AxisCurveMode.Curve3d);
      var actualNorm = StationFactory.Create(new ByEqualSegmentsStrategy(n), mapper).ToArray();

      actualNorm.Should().HaveSameCount(expectedNorm);
      for (int i = 0; i < expectedNorm.Length; i++)
        actualNorm[i].Should().BeApproximately(expectedNorm[i], tol, $"normalized station {i}");

      var actualReal = actualNorm.Select(s => mapper.NormalizedToReal(s)).ToArray();
      actualReal.Should().HaveSameCount(expectedReal);
      for (int i = 0; i < expectedReal.Length; i++)
        actualReal[i].Should().BeApproximately(expectedReal[i], tol, $"real station {i}");
    }
  }
}
