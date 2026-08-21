using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProgesiCore;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>Builds the station table layout (ID | S | values per object).</summary>
  public static class AxisTableLayoutEngine
  {
    public sealed class TableSeries
    {
      public TableSeries(ProgesiAxisVariable axis, CurveParameterMapper mapper, string headerName, string unit)
      {
        Axis = axis ?? throw new ArgumentNullException(nameof(axis));
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        HeaderName = headerName ?? axis.Name;
        Unit = unit ?? string.Empty;
      }

      public ProgesiAxisVariable Axis { get; }
      public CurveParameterMapper Mapper { get; }
      public string HeaderName { get; }
      public string Unit { get; }
    }

    public static TableLayout Build(
      IReadOnlyList<TableSeries> series,
      TableSettings settings,
      LayoutPoint2d origin)
    {
      if (series == null || series.Count == 0)
        throw new ArgumentException("At least one axis series is required.", nameof(series));
      if (settings == null) throw new ArgumentNullException(nameof(settings));

      var rows = CollectRows(series);
      int columnCount = 2 + series.Count;
      int rowCount = rows.Count + 1;

      var cells = new List<TableCellSpec>();

      cells.Add(new TableCellSpec(0, 0, settings.IdColumnHeader, true));
      cells.Add(new TableCellSpec(0, 1, settings.StationColumnHeader, true));
      for (int i = 0; i < series.Count; i++)
      {
        string header = series[i].HeaderName;
        if (settings.ShowUnitsInHeader && !string.IsNullOrWhiteSpace(series[i].Unit))
          header += " (" + series[i].Unit + ")";
        cells.Add(new TableCellSpec(0, 2 + i, header, true));
      }

      for (int r = 0; r < rows.Count; r++)
      {
        var row = rows[r];
        cells.Add(new TableCellSpec(r + 1, 0, row.StationId.ToString(CultureInfo.InvariantCulture), false));
        cells.Add(new TableCellSpec(
          r + 1,
          1,
          row.RealStation.ToString("F" + settings.StationDecimals, CultureInfo.InvariantCulture),
          false));

        for (int c = 0; c < series.Count; c++)
        {
          string text = row.ValuesBySeriesIndex.TryGetValue(c, out var val)
            ? FormatCellValue(val, series[c].Axis.ValueTypeKey, settings.ValueDecimals)
            : string.Empty;
          cells.Add(new TableCellSpec(r + 1, 2 + c, text, false));
        }
      }

      double width = columnCount * settings.CellWidthMm;
      double height = rowCount * settings.CellHeightMm;
      string ids = string.Join(",", series.Select(s => s.Axis.Id.ToString(CultureInfo.InvariantCulture)));

      return new TableLayout(origin, width, height, rowCount, columnCount, cells, ids);
    }

    public static LayoutPoint2d ComputeTableOrigin(DiagramLayout diagram, TableSettings settings)
    {
      double x = diagram.PlotOrigin.X + diagram.PlotWidth + settings.GapFromDiagramMm;
      double y = diagram.PlotOrigin.Y;
      return new LayoutPoint2d(x, y);
    }

    private static string FormatCellValue(object value, string valueTypeKey, int decimals)
    {
      if (value == null)
        return string.Empty;

      if (string.Equals(valueTypeKey, "System.Double", StringComparison.Ordinal) ||
          string.Equals(valueTypeKey, "System.Single", StringComparison.Ordinal) ||
          string.Equals(valueTypeKey, "System.Int32", StringComparison.Ordinal))
      {
        if (AxisDiagramLayoutEngineComputeHelper.TryToDouble(value, out double numeric))
          return numeric.ToString("F" + decimals, CultureInfo.InvariantCulture);
      }

      return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static List<TableRow> CollectRows(IReadOnlyList<TableSeries> series)
    {
      var byReal = new SortedDictionary<double, TableRow>();

      for (int seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
      {
        var s = series[seriesIndex];
        var axis = s.Axis;
        var mapper = s.Mapper;
        var keyPoints = axis.KeyPoints.OrderBy(x => x).Distinct().ToList();
        if (keyPoints.Count == 0)
          keyPoints = new List<double> { 0.0, 1.0 };

        for (int i = 0; i < keyPoints.Count; i++)
        {
          double norm = keyPoints[i];
          double real = mapper.NormalizedToReal(norm);
          real = Math.Round(real, 6);

          if (!byReal.TryGetValue(real, out var row))
          {
            row = new TableRow { RealStation = real, StationId = i + 1 };
            byReal[real] = row;
          }

          var eval = AxisVarGhSupport.EvaluateInterpolateValue(axis, norm);
          row.ValuesBySeriesIndex[seriesIndex] = eval.Value ?? string.Empty;
        }
      }

      return byReal.Values.OrderBy(r => r.RealStation).Select((r, idx) =>
      {
        r.StationId = idx + 1;
        return r;
      }).ToList();
    }

    private sealed class TableRow
    {
      public int StationId { get; set; }
      public double RealStation { get; set; }
      public Dictionary<int, object> ValuesBySeriesIndex { get; } = new Dictionary<int, object>();
    }
  }

  /// <summary>Shared numeric parsing helper for table formatting.</summary>
  internal static class AxisDiagramLayoutEngineComputeHelper
  {
    internal static bool TryToDouble(object value, out double numeric)
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
