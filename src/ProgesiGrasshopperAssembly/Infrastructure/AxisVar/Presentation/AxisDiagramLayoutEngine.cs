using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProgesiCore;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>
  /// Builds a geometry-light 2D diagram layout from axis series, scales, and settings.
  /// </summary>
  public static class AxisDiagramLayoutEngine
  {
    private const int AxisColor = unchecked((int)0xFF000000);
    private const int GridColor = unchecked((int)0xFFB0B0B0);
    private const int TextColor = unchecked((int)0xFF000000);
    private const double TickLengthMm = 2.0;
    private const double TextHeightMm = 2.5;
    private const double LegendLineMm = 8.0;
    private const double LegendRowMm = 5.0;

    public static DiagramLayout Build(
      IReadOnlyList<AxisDiagramSeries> series,
      ScaleModel scale,
      DiagramSettings settings)
    {
      if (series == null || series.Count == 0)
        throw new ArgumentException("At least one axis series is required.", nameof(series));
      if (scale == null) throw new ArgumentNullException(nameof(scale));
      if (settings == null) throw new ArgumentNullException(nameof(settings));

      var lines = new List<LayoutLineSpec>();
      var polylines = new List<LayoutPolylineSpec>();
      var texts = new List<LayoutTextSpec>();
      var legend = new List<LayoutLegendItemSpec>();

      var plotOrigin = new LayoutPoint2d(scale.OriginOffsetX, scale.OriginOffsetY);
      double plotLeft = plotOrigin.X;
      double plotBottom = plotOrigin.Y;
      double plotRight = plotLeft + scale.PlotWidth;
      double plotTop = plotBottom + scale.PlotHeight;

      // Baseline axes
      lines.Add(new LayoutLineSpec(
        new LayoutPoint2d(plotLeft, plotBottom),
        new LayoutPoint2d(plotRight, plotBottom),
        AxisColor, 0.25, false));
      lines.Add(new LayoutLineSpec(
        new LayoutPoint2d(plotLeft, plotBottom),
        new LayoutPoint2d(plotLeft, plotTop),
        AxisColor, 0.25, false));

      AddTicksAndLabels(scale, settings, lines, texts, plotLeft, plotBottom, plotRight, plotTop);

      if (settings.GridEnabled)
        AddGridlines(scale, lines, plotLeft, plotBottom, plotRight, plotTop);

      for (int i = 0; i < series.Count; i++)
      {
        var s = series[i];
        int color = ResolveSeriesColor(settings, i);
        BuildSeriesGeometry(s, scale, settings, color, i, lines, polylines);
        if (settings.ShowLegend)
        {
          legend.Add(new LayoutLegendItemSpec(
            s.LegendLabel,
            color,
            new LayoutPoint2d(plotRight + 5.0, plotTop - i * LegendRowMm)));
        }
      }

      if (settings.ShowTitles)
        AddTitles(settings, texts, plotLeft, plotRight, plotTop);

      string ids = string.Join(",", series.Select(x => x.Axis.Id.ToString(CultureInfo.InvariantCulture)));
      string mode = series[0].Mapper.Mode.ToString();

      return new DiagramLayout(
        plotOrigin,
        scale.PlotWidth,
        scale.PlotHeight,
        lines,
        polylines,
        texts,
        legend,
        ids,
        mode);
    }

    public static (double OrdinateMin, double OrdinateMax) ComputeOrdinateRange(IReadOnlyList<AxisDiagramSeries> series)
    {
      double min = double.PositiveInfinity;
      double max = double.NegativeInfinity;

      foreach (var s in series)
      {
        foreach (var sample in SampleSeries(s, 16))
        {
          if (!sample.IsNumeric)
            continue;
          min = Math.Min(min, sample.NumericValue);
          max = Math.Max(max, sample.NumericValue);
        }
      }

      if (double.IsInfinity(min) || double.IsInfinity(max))
        return (0.0, 1.0);

      return (min, max);
    }

    private static void BuildSeriesGeometry(
      AxisDiagramSeries series,
      ScaleModel scale,
      DiagramSettings settings,
      int color,
      int seriesIndex,
      IList<LayoutLineSpec> lines,
      IList<LayoutPolylineSpec> polylines)
    {
      var samples = SampleSeries(series, settings.SampleCountPerSegment);
      if (samples.Count == 0)
        return;

      var points = new List<LayoutPoint2d>();
      for (int i = 0; i < samples.Count; i++)
      {
        var sample = samples[i];
        if (!sample.IsNumeric)
          continue;

        points.Add(DataToLayout(sample.RealStation, sample.NumericValue, scale));

        if (sample.IsDiscontinuity && i > 0)
        {
          var prev = samples[i - 1];
          if (prev.IsNumeric && Math.Abs(prev.NumericValue - sample.NumericValue) > ProgesiAxisVariable.DefaultTolerance)
          {
            var x = DataToLayout(sample.RealStation, 0, scale).X;
            var y0 = DataToLayout(sample.RealStation, prev.NumericValue, scale).Y;
            var y1 = DataToLayout(sample.RealStation, sample.NumericValue, scale).Y;
            lines.Add(new LayoutLineSpec(
              new LayoutPoint2d(x, Math.Min(y0, y1)),
              new LayoutPoint2d(x, Math.Max(y0, y1)),
              color, 0.2, false));
          }
        }
      }

      if (points.Count >= 2)
        polylines.Add(new LayoutPolylineSpec(points, color, 0.35, seriesIndex));
    }

    private static void AddTicksAndLabels(
      ScaleModel scale,
      DiagramSettings settings,
      IList<LayoutLineSpec> lines,
      IList<LayoutTextSpec> texts,
      double plotLeft,
      double plotBottom,
      double plotRight,
      double plotTop)
    {
      AddAxisTicks(
        scale.AbscissaMin,
        scale.AbscissaMax,
        scale.MajorTickX,
        scale.MinorTickX,
        scale.ScaleX,
        true,
        settings.StationDecimals,
        plotLeft,
        plotBottom,
        plotRight,
        lines,
        texts);

      AddAxisTicks(
        scale.OrdinateMin,
        scale.OrdinateMax,
        scale.MajorTickY,
        scale.MinorTickY,
        scale.ScaleY,
        false,
        settings.ValueDecimals,
        plotLeft,
        plotBottom,
        plotTop,
        lines,
        texts);

      // Station IDs on abscissa at keypoints from first series is handled via major ticks;
      // additional station-id labels at integer indices when keypoints align.
    }

    private static void AddAxisTicks(
      double dataMin,
      double dataMax,
      double majorStep,
      double minorStep,
      double scaleFactor,
      bool horizontal,
      int decimals,
      double plotLeft,
      double plotBottom,
      double plotEdge,
      IList<LayoutLineSpec> lines,
      IList<LayoutTextSpec> texts)
    {
      if (majorStep <= 0)
        return;

      double start = Math.Floor(dataMin / majorStep) * majorStep;
      for (double v = start; v <= dataMax + majorStep * 0.5; v += majorStep)
      {
        if (v < dataMin - ProgesiAxisVariable.DefaultTolerance)
          continue;

        double pos = horizontal
          ? plotLeft + (v - dataMin) * scaleFactor
          : plotBottom + (v - dataMin) * scaleFactor;

        if (horizontal)
        {
          lines.Add(new LayoutLineSpec(
            new LayoutPoint2d(pos, plotBottom),
            new LayoutPoint2d(pos, plotBottom - TickLengthMm),
            AxisColor, 0.15, false));
          texts.Add(new LayoutTextSpec(
            new LayoutPoint2d(pos, plotBottom - TickLengthMm - TextHeightMm),
            FormatValue(v, decimals),
            TextColor,
            TextHeightMm,
            true));
        }
        else
        {
          lines.Add(new LayoutLineSpec(
            new LayoutPoint2d(plotLeft - TickLengthMm, pos),
            new LayoutPoint2d(plotLeft, pos),
            AxisColor, 0.15, false));
          texts.Add(new LayoutTextSpec(
            new LayoutPoint2d(plotLeft - TickLengthMm - 1.0, pos - TextHeightMm * 0.35),
            FormatValue(v, decimals),
            TextColor,
            TextHeightMm,
            true));
        }
      }

      if (minorStep <= 0)
        return;

      double minorStart = Math.Floor(dataMin / minorStep) * minorStep;
      for (double v = minorStart; v <= dataMax + minorStep * 0.5; v += minorStep)
      {
        if (Math.Abs(v / majorStep - Math.Round(v / majorStep)) < 1e-6)
          continue;
        if (v < dataMin - ProgesiAxisVariable.DefaultTolerance)
          continue;

        double pos = horizontal
          ? plotLeft + (v - dataMin) * scaleFactor
          : plotBottom + (v - dataMin) * scaleFactor;

        if (horizontal)
        {
          lines.Add(new LayoutLineSpec(
            new LayoutPoint2d(pos, plotBottom),
            new LayoutPoint2d(pos, plotBottom - TickLengthMm * 0.6),
            AxisColor, 0.1, false));
        }
        else
        {
          lines.Add(new LayoutLineSpec(
            new LayoutPoint2d(plotLeft - TickLengthMm * 0.6, pos),
            new LayoutPoint2d(plotLeft, pos),
            AxisColor, 0.1, false));
        }
      }
    }

    private static void AddGridlines(
      ScaleModel scale,
      IList<LayoutLineSpec> lines,
      double plotLeft,
      double plotBottom,
      double plotRight,
      double plotTop)
    {
      double x = scale.AbscissaMin;
      while (x <= scale.AbscissaMax + scale.MajorTickX * 0.5)
      {
        double px = plotLeft + (x - scale.AbscissaMin) * scale.ScaleX;
        lines.Add(new LayoutLineSpec(
          new LayoutPoint2d(px, plotBottom),
          new LayoutPoint2d(px, plotTop),
          GridColor, 0.1, true));
        x += scale.MajorTickX;
      }

      double y = scale.OrdinateMin;
      while (y <= scale.OrdinateMax + scale.MajorTickY * 0.5)
      {
        double py = plotBottom + (y - scale.OrdinateMin) * scale.ScaleY;
        lines.Add(new LayoutLineSpec(
          new LayoutPoint2d(plotLeft, py),
          new LayoutPoint2d(plotRight, py),
          GridColor, 0.1, true));
        y += scale.MajorTickY;
      }
    }

    private static void AddTitles(
      DiagramSettings settings,
      IList<LayoutTextSpec> texts,
      double plotLeft,
      double plotRight,
      double plotTop)
    {
      string abscissa = settings.AbscissaTitle;
      if (!string.IsNullOrWhiteSpace(settings.AbscissaUnit))
        abscissa += " (" + settings.AbscissaUnit + ")";

      string ordinate = settings.OrdinateTitle;
      if (!string.IsNullOrWhiteSpace(settings.OrdinateUnit))
        ordinate += " (" + settings.OrdinateUnit + ")";

      texts.Add(new LayoutTextSpec(
        new LayoutPoint2d((plotLeft + plotRight) * 0.5, plotTop + TextHeightMm * 2.0),
        abscissa,
        TextColor,
        TextHeightMm,
        false));

      texts.Add(new LayoutTextSpec(
        new LayoutPoint2d(plotLeft - TextHeightMm * 4.0, plotTop * 0.5),
        ordinate,
        TextColor,
        TextHeightMm,
        false));
    }

    private static LayoutPoint2d DataToLayout(double realStation, double value, ScaleModel scale)
    {
      double x = scale.OriginOffsetX + (realStation - scale.AbscissaMin) * scale.ScaleX;
      double y = scale.OriginOffsetY + (value - scale.OrdinateMin) * scale.ScaleY;
      return new LayoutPoint2d(x, y);
    }

    private static int ResolveSeriesColor(DiagramSettings settings, int index)
    {
      var palette = settings.SeriesColorsArgb;
      if (palette == null || palette.Length == 0)
        return AxisColor;
      return palette[index % palette.Length];
    }

    private static string FormatValue(double value, int decimals)
      => value.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);

    private sealed class SeriesSample
    {
      public double RealStation { get; set; }
      public double NormalizedStation { get; set; }
      public bool IsNumeric { get; set; }
      public double NumericValue { get; set; }
      public bool IsDiscontinuity { get; set; }
    }

    private static List<SeriesSample> SampleSeries(AxisDiagramSeries series, int samplesPerSegment)
    {
      var axis = series.Axis;
      var mapper = series.Mapper;
      var normalizedStations = CollectSampleStations(axis, samplesPerSegment);
      var result = new List<SeriesSample>();

      double? prevNumeric = null;
      foreach (var norm in normalizedStations)
      {
        double real = mapper.NormalizedToReal(norm);
        var eval = AxisVarGhSupport.EvaluateInterpolateValue(axis, norm);
        bool isNumeric = TryToDouble(eval.Value, out double numeric);

        bool isDisc = axis.KeyPoints.Any(kp => Math.Abs(kp - norm) <= ProgesiAxisVariable.DefaultTolerance);
        if (isDisc && prevNumeric.HasValue && isNumeric)
          isDisc = Math.Abs(prevNumeric.Value - numeric) > ProgesiAxisVariable.DefaultTolerance;
        else if (isDisc)
          isDisc = false;

        result.Add(new SeriesSample
        {
          RealStation = real,
          NormalizedStation = norm,
          IsNumeric = isNumeric,
          NumericValue = numeric,
          IsDiscontinuity = isDisc
        });

        if (isNumeric)
          prevNumeric = numeric;
      }

      return result;
    }

    private static List<double> CollectSampleStations(ProgesiAxisVariable axis, int samplesPerSegment)
    {
      var keyPoints = axis.KeyPoints.OrderBy(x => x).Distinct().ToList();
      if (keyPoints.Count == 0)
        keyPoints = new List<double> { 0.0, 1.0 };

      var stations = new List<double>();
      for (int i = 0; i < keyPoints.Count - 1; i++)
      {
        double a = keyPoints[i];
        double b = keyPoints[i + 1];
        int steps = Math.Max(1, samplesPerSegment);
        for (int s = 0; s <= steps; s++)
        {
          if (i > 0 && s == 0)
            continue;
          double t = s / (double)steps;
          stations.Add(a + (b - a) * t);
        }
      }

      if (stations.Count == 0 || Math.Abs(stations[stations.Count - 1] - keyPoints[keyPoints.Count - 1]) > ProgesiAxisVariable.DefaultTolerance)
        stations.Add(keyPoints[keyPoints.Count - 1]);

      // Segment boundaries for discontinuity markers
      foreach (var seg in GetFunctionSegmentBounds(axis))
      {
        if (!stations.Any(x => Math.Abs(x - seg) <= ProgesiAxisVariable.DefaultTolerance))
          stations.Add(seg);
      }

      return stations.OrderBy(x => x).Distinct().ToList();
    }

    private static IEnumerable<double> GetFunctionSegmentBounds(ProgesiAxisVariable axis)
    {
      if (axis.FunctionRef.IsEmpty || axis.FunctionRef.Embedded == null)
        yield break;

      foreach (var seg in axis.FunctionRef.Embedded.Segments)
      {
        yield return seg.Start;
        yield return seg.End;
      }
    }

    private static bool TryToDouble(object value, out double numeric)
    {
      numeric = 0.0;
      if (value == null)
        return false;

      switch (value)
      {
        case double d:
          numeric = d;
          return true;
        case float f:
          numeric = f;
          return true;
        case int i:
          numeric = i;
          return true;
        case long l:
          numeric = l;
          return true;
        case decimal m:
          numeric = (double)m;
          return true;
        default:
          return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out numeric);
      }
    }
  }
}
