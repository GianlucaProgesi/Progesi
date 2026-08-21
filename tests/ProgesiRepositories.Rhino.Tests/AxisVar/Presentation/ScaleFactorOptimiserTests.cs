using FluentAssertions;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar.Presentation
{
  public sealed class ScaleFactorOptimiserTests
  {
    [Fact]
    public void NiceStep_Rounds_To_1_2_5_Powers_Of_Ten()
    {
      ScaleFactorOptimiser.NiceStep(35.0, 5).Should().Be(10.0);
      ScaleFactorOptimiser.NiceStep(98.968639, 10).Should().Be(10.0);
      ScaleFactorOptimiser.NiceStep(0.7, 5).Should().Be(0.2);
    }

    [Fact]
    public void Optimise_SsotRanges_IndependentScales_And_NiceMinorHalfMajor()
    {
      const double l = 98.968639;
      var settings = new DiagramSettings
      {
        TargetBoxWidthMm = 200.0,
        TargetBoxHeightMm = 120.0,
        OriginOffsetMm = 10.0,
        TargetTickCount = 5
      };

      var scale = ScaleFactorOptimiser.Optimise(l, 30.0, 65.0, settings);

      scale.AbscissaMax.Should().BeApproximately(l, 1e-9);
      scale.OrdinateMin.Should().Be(30.0);
      scale.OrdinateMax.Should().Be(65.0);
      scale.OriginOffsetX.Should().Be(10.0);
      scale.OriginOffsetY.Should().Be(10.0);

      scale.ScaleX.Should().BeApproximately(180.0 / l, 1e-6);
      scale.ScaleY.Should().BeApproximately(100.0 / 35.0, 1e-6);
      scale.ScaleX.Should().NotBe(scale.ScaleY);

      scale.MajorTickY.Should().Be(10.0);
      scale.MinorTickY.Should().Be(5.0);
      scale.MinorTickX.Should().Be(scale.MajorTickX * 0.5);

      scale.PlotWidth.Should().BeApproximately(l * scale.ScaleX, 1e-6);
      scale.PlotHeight.Should().BeApproximately(35.0 * scale.ScaleY, 1e-6);
    }
  }
}
