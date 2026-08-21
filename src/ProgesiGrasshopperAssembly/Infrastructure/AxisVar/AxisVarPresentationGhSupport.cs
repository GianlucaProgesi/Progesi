#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using ProgesiRepositories.Rhino;
using Rhino;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>GH orchestration for B3c-2 diagram/table components (consumes B3c-1 services).</summary>
  public static class AxisVarPresentationGhSupport
  {
    internal static void PrepareOptionalListInput(GH_Component owner, int inputIndex)
    {
      if (inputIndex < 0 || inputIndex >= owner.Params.Input.Count)
        return;

      var param = owner.Params.Input[inputIndex];
      if (param.Optional && param.SourceCount == 0)
        param.ClearData();
    }

    internal static DiagramSettings DefaultDiagramSettings() => new DiagramSettings();

    internal static TableSettings DefaultTableSettings() => new TableSettings();

    internal static bool TryUnwrapSettings<T>(object? input, out T settings) where T : class
    {
      settings = null!;
      if (input == null)
        return false;

      if (input is GH_ObjectWrapper ow && ow.Value is T typed)
      {
        settings = typed;
        return true;
      }

      if (input is IGH_Goo goo)
      {
        var script = goo.ScriptVariable();
        if (script is T typedScript)
        {
          settings = typedScript;
          return true;
        }
      }

      return false;
    }

    internal static DiagramSettings ResolveDiagramSettings(object? wiredSettings, DiagramSettings? overrides = null)
    {
      var settings = DefaultDiagramSettings();
      if (TryUnwrapSettings(wiredSettings, out DiagramSettings? fromWire))
      {
        CopyDiagramSettings(fromWire, settings);
      }

      if (overrides != null)
        CopyDiagramSettings(overrides, settings);

      return settings;
    }

    internal static TableSettings ResolveTableSettings(object? wiredSettings, TableSettings? overrides = null)
    {
      var (settings, _) = ResolveTableSettingsWithUnit(wiredSettings, overrides);
      return settings;
    }

    internal static (TableSettings Settings, string DefaultUnit) ResolveTableSettingsWithUnit(
      object? wiredSettings,
      TableSettings? overrides = null)
    {
      var settings = DefaultTableSettings();
      string defaultUnit = string.Empty;

      if (wiredSettings is GH_ObjectWrapper ow)
        wiredSettings = ow.Value;
      else if (wiredSettings is IGH_Goo goo)
        wiredSettings = goo.ScriptVariable();

      if (wiredSettings is TableSettingsBundle bundle)
      {
        CopyTableSettings(bundle.Settings, settings);
        defaultUnit = bundle.DefaultUnit;
      }
      else if (wiredSettings is TableSettings fromWire)
        CopyTableSettings(fromWire, settings);

      if (overrides != null)
        CopyTableSettings(overrides, settings);

      return (settings, defaultUnit);
    }

    internal static bool TryLoadAxes(
      IGH_DataAccess da,
      int axesListIndex,
      int idsListIndex,
      GH_Component owner,
      RhinoAxisVariableRepository repo,
      out List<ProgesiAxisVariable> axes)
    {
      axes = new List<ProgesiAxisVariable>();
      var seenIds = new HashSet<int>();

      var gooList = new List<IGH_Goo>();
      if (da.GetDataList(axesListIndex, gooList))
      {
        foreach (var goo in gooList)
        {
          if (goo == null)
            continue;

          object? item = goo;
          if (goo is GH_ObjectWrapper ow)
            item = ow.Value;
          else
            item = goo.ScriptVariable();

          if (item == null)
            continue;

          if (item is AxisVarHandle handle)
          {
            if (seenIds.Add(handle.AxisId))
              axes.Add(handle.Axis);
            continue;
          }

          if (item is ProgesiAxisVariable direct)
          {
            if (seenIds.Add(direct.Id))
              axes.Add(direct);
            continue;
          }

          if (item is int axisId && axisId > 0)
          {
            var loaded = repo.GetByIdAsync(axisId).GetAwaiter().GetResult();
            if (loaded == null)
            {
              owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Axis id {axisId} not found.");
              return false;
            }

            if (seenIds.Add(loaded.Id))
              axes.Add(loaded);
            continue;
          }

          owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axes list entries must be AxisVarHandle or positive Id.");
          return false;
        }
      }

      var idList = new List<int>();
      if (da.GetDataList(idsListIndex, idList))
      {
        foreach (var id in idList)
        {
          if (id <= 0)
            continue;

          if (!seenIds.Add(id))
            continue;

          var loaded = repo.GetByIdAsync(id).GetAwaiter().GetResult();
          if (loaded == null)
          {
            owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Axis id {id} not found.");
            return false;
          }

          axes.Add(loaded);
        }
      }

      if (axes.Count == 0)
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one axis handle or positive Id is required.");
        return false;
      }

      return true;
    }

    internal static IReadOnlyList<int>? ReadOptionalColours(IGH_DataAccess da, int inputIndex)
    {
      var gooList = new List<IGH_Goo>();
      if (!da.GetDataList(inputIndex, gooList) || gooList.Count == 0)
        return null;

      var colours = new List<int>();
      foreach (var goo in gooList)
      {
        if (goo == null)
          continue;

        if (goo is GH_Colour ghColour)
        {
          colours.Add(ghColour.Value.ToArgb());
          continue;
        }

        if (goo.CastTo(out Color color))
        {
          colours.Add(color.ToArgb());
          continue;
        }

        if (goo.CastTo(out int argb))
        {
          colours.Add(argb);
          continue;
        }
      }

      return colours.Count == 0 ? null : colours;
    }

    internal static void ApplySeriesColours(DiagramSettings settings, IReadOnlyList<int>? wiredColours)
    {
      if (wiredColours == null || wiredColours.Count == 0)
        return;

      settings.SeriesColorsArgb = wiredColours.ToArray();
    }

    public static PresentationDiagramResult BakeDiagram(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      DiagramSettings settings,
      Point3d insertionPoint)
      => BakeDiagramCore(doc, axes, settings, insertionPoint, owner: null);

    internal static PresentationDiagramResult BakeDiagram(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      DiagramSettings settings,
      Point3d insertionPoint,
      GH_Component owner)
      => BakeDiagramCore(doc, axes, settings, insertionPoint, owner);

    private static PresentationDiagramResult BakeDiagramCore(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      DiagramSettings settings,
      Point3d insertionPoint,
      GH_Component? owner)
    {
      var series = BuildDiagramSeries(axes, settings, owner);
      double abscissaMax = series.Max(s => s.Axis.AxisLength ?? s.Mapper.TotalLength);
      var (ordMin, ordMax) = AxisDiagramLayoutEngine.ComputeOrdinateRange(series);
      var scale = ScaleFactorOptimiser.Optimise(abscissaMax, ordMin, ordMax, settings);
      var layout = AxisDiagramLayoutEngine.Build(series, scale, settings);
      string blockName = BuildBlockName("Progesi.AxisVar.Diagram", axes);
      var baked = Rhino2DBlockBuilder.BakeDiagram(doc, layout, scale, insertionPoint, blockName);
      return new PresentationDiagramResult(baked, scale, layout);
    }

    public static PresentationBlockResult BakeTable(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      TableSettings settings,
      Point3d insertionPoint,
      string? defaultUnit = null)
      => BakeTableCore(doc, axes, settings, insertionPoint, owner: null, defaultUnit);

    internal static PresentationBlockResult BakeTable(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      TableSettings settings,
      Point3d insertionPoint,
      GH_Component owner,
      string? defaultUnit = null)
      => BakeTableCore(doc, axes, settings, insertionPoint, owner, defaultUnit);

    private static PresentationBlockResult BakeTableCore(
      RhinoDoc doc,
      IReadOnlyList<ProgesiAxisVariable> axes,
      TableSettings settings,
      Point3d insertionPoint,
      GH_Component? owner,
      string? defaultUnit)
    {
      var tableSeries = BuildTableSeries(axes, settings, owner, defaultUnit);
      var layout = AxisTableLayoutEngine.Build(tableSeries, settings, new LayoutPoint2d(0, 0));
      string modeName = tableSeries[0].Mapper.Mode.ToString();
      string blockName = BuildBlockName("Progesi.AxisVar.Table", axes);
      return Rhino2DBlockBuilder.BakeTable(doc, layout, insertionPoint, blockName, modeName);
    }

    public static List<AxisDiagramSeries> BuildDiagramSeries(
      IReadOnlyList<ProgesiAxisVariable> axes,
      DiagramSettings settings)
      => BuildDiagramSeries(axes, settings, owner: null);

    private static List<AxisDiagramSeries> BuildDiagramSeries(
      IReadOnlyList<ProgesiAxisVariable> axes,
      DiagramSettings settings,
      GH_Component? owner)
    {
      var series = new List<AxisDiagramSeries>();
      for (int i = 0; i < axes.Count; i++)
      {
        var axis = axes[i];
        if (!TryDecodeCurveForPresentation(axis, owner, out var curve) || curve == null)
          throw new InvalidOperationException($"Axis id {axis.Id} has no decodable curve.");

        var mapper = AxisVarGhSupport.CreateMapper(curve, axis.Mode);
        int color = ResolveSeriesColorArgb(settings, i);
        series.Add(new AxisDiagramSeries(axis, mapper, color, axis.Name));
      }

      return series;
    }

    public static List<AxisTableLayoutEngine.TableSeries> BuildTableSeries(
      IReadOnlyList<ProgesiAxisVariable> axes,
      TableSettings settings,
      string? defaultUnit)
      => BuildTableSeries(axes, settings, owner: null, defaultUnit);

    private static List<AxisTableLayoutEngine.TableSeries> BuildTableSeries(
      IReadOnlyList<ProgesiAxisVariable> axes,
      TableSettings settings,
      GH_Component? owner,
      string? defaultUnit)
    {
      var series = new List<AxisTableLayoutEngine.TableSeries>();
      foreach (var axis in axes)
      {
        if (!TryDecodeCurveForPresentation(axis, owner, out var curve) || curve == null)
          throw new InvalidOperationException($"Axis id {axis.Id} has no decodable curve.");

        var mapper = AxisVarGhSupport.CreateMapper(curve, axis.Mode);
        series.Add(new AxisTableLayoutEngine.TableSeries(axis, mapper, axis.Name, defaultUnit ?? string.Empty));
      }

      return series;
    }

    private static bool TryDecodeCurveForPresentation(
      ProgesiAxisVariable axis,
      GH_Component? owner,
      out Curve? curve)
    {
      if (owner != null)
        return AxisVarGhSupport.TryDecodeCurve(axis, owner, out curve);

      curve = null;
      if (string.IsNullOrWhiteSpace(axis.CurvePayload))
        return false;

      if (!ProgesiGeometryValueCodec.TryDecode(axis.CurvePayload, out var geom))
        return false;

      var c = geom as Curve;
      if (c == null)
        return false;

      curve = c;
      return true;
    }

    private static int ResolveSeriesColorArgb(DiagramSettings settings, int index)
    {
      var palette = settings.SeriesColorsArgb;
      if (palette == null || palette.Length == 0)
        return unchecked((int)0xFF000000);
      return palette[index % palette.Length];
    }

    private static string BuildBlockName(string prefix, IReadOnlyList<ProgesiAxisVariable> axes)
    {
      string ids = string.Join("-", axes.Select(a => a.Id.ToString()));
      return prefix + "." + ids;
    }

    private static void CopyDiagramSettings(DiagramSettings source, DiagramSettings target)
    {
      target.AutoFit = source.AutoFit;
      target.GridEnabled = source.GridEnabled;
      target.TargetBoxWidthMm = source.TargetBoxWidthMm;
      target.TargetBoxHeightMm = source.TargetBoxHeightMm;
      target.OriginOffsetMm = source.OriginOffsetMm;
      target.TargetTickCount = source.TargetTickCount;
      target.ValueDecimals = source.ValueDecimals;
      target.StationDecimals = source.StationDecimals;
      target.ShowLegend = source.ShowLegend;
      target.ShowTitles = source.ShowTitles;
      target.AbscissaTitle = source.AbscissaTitle;
      target.OrdinateTitle = source.OrdinateTitle;
      target.AbscissaUnit = source.AbscissaUnit;
      target.OrdinateUnit = source.OrdinateUnit;
      target.SampleCountPerSegment = source.SampleCountPerSegment;
      target.GridGreyFactor = source.GridGreyFactor;
      if (source.SeriesColorsArgb != null && source.SeriesColorsArgb.Length > 0)
        target.SeriesColorsArgb = (int[])source.SeriesColorsArgb.Clone();
    }

    private static void CopyTableSettings(TableSettings source, TableSettings target)
    {
      target.ValueDecimals = source.ValueDecimals;
      target.StationDecimals = source.StationDecimals;
      target.ShowUnitsInHeader = source.ShowUnitsInHeader;
      target.StationColumnHeader = source.StationColumnHeader;
      target.IdColumnHeader = source.IdColumnHeader;
      target.CellWidthMm = source.CellWidthMm;
      target.CellHeightMm = source.CellHeightMm;
      target.TextHeightMm = source.TextHeightMm;
      target.GapFromDiagramMm = source.GapFromDiagramMm;
    }
  }

  public sealed class PresentationDiagramResult
  {
    public PresentationDiagramResult(
      PresentationBlockResult block,
      ScaleModel scale,
      DiagramLayout layout)
    {
      Block = block;
      Scale = scale;
      Layout = layout;
    }

    public PresentationBlockResult Block { get; }
    public ScaleModel Scale { get; }
    public DiagramLayout Layout { get; }
  }
}
