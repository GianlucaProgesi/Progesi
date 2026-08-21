using System.Collections.Generic;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  public readonly struct LayoutPoint2d
  {
    public LayoutPoint2d(double x, double y)
    {
      X = x;
      Y = y;
    }

    public double X { get; }
    public double Y { get; }
  }

  public sealed class LayoutLineSpec
  {
    public LayoutLineSpec(LayoutPoint2d start, LayoutPoint2d end, int colorArgb, double thickness, bool isGrid)
    {
      Start = start;
      End = end;
      ColorArgb = colorArgb;
      Thickness = thickness;
      IsGrid = isGrid;
    }

    public LayoutPoint2d Start { get; }
    public LayoutPoint2d End { get; }
    public int ColorArgb { get; }
    public double Thickness { get; }
    public bool IsGrid { get; }
  }

  public sealed class LayoutPolylineSpec
  {
    public LayoutPolylineSpec(IReadOnlyList<LayoutPoint2d> points, int colorArgb, double thickness, int seriesIndex)
    {
      Points = points;
      ColorArgb = colorArgb;
      Thickness = thickness;
      SeriesIndex = seriesIndex;
    }

    public IReadOnlyList<LayoutPoint2d> Points { get; }
    public int ColorArgb { get; }
    public double Thickness { get; }
    public int SeriesIndex { get; }
  }

  public sealed class LayoutTextSpec
  {
    public LayoutTextSpec(LayoutPoint2d position, string text, int colorArgb, double heightMm, bool isTickLabel)
    {
      Position = position;
      Text = text ?? string.Empty;
      ColorArgb = colorArgb;
      HeightMm = heightMm;
      IsTickLabel = isTickLabel;
    }

    public LayoutPoint2d Position { get; }
    public string Text { get; }
    public int ColorArgb { get; }
    public double HeightMm { get; }
    public bool IsTickLabel { get; }
  }

  public sealed class LayoutLegendItemSpec
  {
    public LayoutLegendItemSpec(string label, int colorArgb, LayoutPoint2d swatchOrigin)
    {
      Label = label ?? string.Empty;
      ColorArgb = colorArgb;
      SwatchOrigin = swatchOrigin;
    }

    public string Label { get; }
    public int ColorArgb { get; }
    public LayoutPoint2d SwatchOrigin { get; }
  }

  public sealed class DiagramLayout
  {
    public DiagramLayout(
      LayoutPoint2d plotOrigin,
      double plotWidth,
      double plotHeight,
      IReadOnlyList<LayoutLineSpec> lines,
      IReadOnlyList<LayoutPolylineSpec> polylines,
      IReadOnlyList<LayoutTextSpec> texts,
      IReadOnlyList<LayoutLegendItemSpec> legendItems,
      string objectIdsCsv,
      string modeName)
    {
      PlotOrigin = plotOrigin;
      PlotWidth = plotWidth;
      PlotHeight = plotHeight;
      Lines = lines;
      Polylines = polylines;
      Texts = texts;
      LegendItems = legendItems;
      ObjectIdsCsv = objectIdsCsv ?? string.Empty;
      ModeName = modeName ?? string.Empty;
    }

    public LayoutPoint2d PlotOrigin { get; }
    public double PlotWidth { get; }
    public double PlotHeight { get; }
    public IReadOnlyList<LayoutLineSpec> Lines { get; }
    public IReadOnlyList<LayoutPolylineSpec> Polylines { get; }
    public IReadOnlyList<LayoutTextSpec> Texts { get; }
    public IReadOnlyList<LayoutLegendItemSpec> LegendItems { get; }
    public string ObjectIdsCsv { get; }
    public string ModeName { get; }
  }
}
