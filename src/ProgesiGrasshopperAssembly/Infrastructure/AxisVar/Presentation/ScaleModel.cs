namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>Resolved independent X/Y scales and tick spacing for diagram layout.</summary>
  public sealed class ScaleModel
  {
    public ScaleModel(
      double scaleX,
      double scaleY,
      double majorTickX,
      double minorTickX,
      double majorTickY,
      double minorTickY,
      double originOffsetX,
      double originOffsetY,
      double plotWidth,
      double plotHeight,
      double abscissaMin,
      double abscissaMax,
      double ordinateMin,
      double ordinateMax)
    {
      ScaleX = scaleX;
      ScaleY = scaleY;
      MajorTickX = majorTickX;
      MinorTickX = minorTickX;
      MajorTickY = majorTickY;
      MinorTickY = minorTickY;
      OriginOffsetX = originOffsetX;
      OriginOffsetY = originOffsetY;
      PlotWidth = plotWidth;
      PlotHeight = plotHeight;
      AbscissaMin = abscissaMin;
      AbscissaMax = abscissaMax;
      OrdinateMin = ordinateMin;
      OrdinateMax = ordinateMax;
    }

    /// <summary>World mm per real-station unit (typically metres).</summary>
    public double ScaleX { get; }

    /// <summary>World mm per ordinate value unit.</summary>
    public double ScaleY { get; }

    public double MajorTickX { get; }
    public double MinorTickX { get; }
    public double MajorTickY { get; }
    public double MinorTickY { get; }
    public double OriginOffsetX { get; }
    public double OriginOffsetY { get; }
    public double PlotWidth { get; }
    public double PlotHeight { get; }
    public double AbscissaMin { get; }
    public double AbscissaMax { get; }
    public double OrdinateMin { get; }
    public double OrdinateMax { get; }
  }
}
