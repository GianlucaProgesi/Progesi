using System;
using System.Collections.Generic;
using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  public sealed class PresentationBlockResult
  {
    public PresentationBlockResult(Guid instanceId, string blockName, Guid definitionId)
    {
      InstanceId = instanceId;
      BlockName = blockName ?? string.Empty;
      DefinitionId = definitionId;
    }

    public Guid InstanceId { get; }
    public string BlockName { get; }
    public Guid DefinitionId { get; }
  }

  /// <summary>Bakes diagram/table layout DTOs as named Rhino blocks with tagged attributes.</summary>
  public static class Rhino2DBlockBuilder
  {
    private const double TextPlaneOffsetZ = 0.0;

    public static PresentationBlockResult BakeDiagram(
      RhinoDoc doc,
      DiagramLayout layout,
      ScaleModel scale,
      Point3d insertionPoint,
      string blockName)
    {
      if (doc == null) throw new ArgumentNullException(nameof(doc));
      if (layout == null) throw new ArgumentNullException(nameof(layout));
      if (scale == null) throw new ArgumentNullException(nameof(scale));

      var geometry = new List<GeometryBase>();
      var attributes = new List<ObjectAttributes>();

      foreach (var line in layout.Lines)
        AddLine(geometry, attributes, line);

      foreach (var pl in layout.Polylines)
        AddPolyline(geometry, attributes, pl);

      foreach (var text in layout.Texts)
        AddText(geometry, attributes, text);

      foreach (var legend in layout.LegendItems)
      {
        AddLine(geometry, attributes, new LayoutLineSpec(
          legend.SwatchOrigin,
          new LayoutPoint2d(legend.SwatchOrigin.X + 8.0, legend.SwatchOrigin.Y),
          legend.ColorArgb,
          0.35,
          false));
        AddText(geometry, attributes, new LayoutTextSpec(
          new LayoutPoint2d(legend.SwatchOrigin.X + 10.0, legend.SwatchOrigin.Y - 0.5),
          legend.Label,
          unchecked((int)0xFF000000),
          2.0,
          false));
      }

      string anchor = FormatAnchor(insertionPoint);
      var tags = new Dictionary<string, string>
      {
        [PresentationBlockTags.BlockKind] = PresentationBlockTags.KindDiagram,
        [PresentationBlockTags.ObjectIds] = layout.ObjectIdsCsv,
        [PresentationBlockTags.Mode] = layout.ModeName,
        [PresentationBlockTags.ScaleX] = scale.ScaleX.ToString("R", CultureInfo.InvariantCulture),
        [PresentationBlockTags.ScaleY] = scale.ScaleY.ToString("R", CultureInfo.InvariantCulture),
        [PresentationBlockTags.AnchorFrame] = anchor
      };

      return Bake(doc, geometry, attributes, blockName, insertionPoint, tags);
    }

    public static PresentationBlockResult BakeTable(
      RhinoDoc doc,
      TableLayout layout,
      Point3d insertionPoint,
      string blockName,
      string modeName)
    {
      if (doc == null) throw new ArgumentNullException(nameof(doc));
      if (layout == null) throw new ArgumentNullException(nameof(layout));

      var geometry = new List<GeometryBase>();
      var attributes = new List<ObjectAttributes>();

      double cellW = layout.Width / layout.ColumnCount;
      double cellH = layout.Height / layout.RowCount;

      for (int c = 0; c <= layout.ColumnCount; c++)
      {
        double x = layout.Origin.X + c * cellW;
        AddLine(geometry, attributes, new LayoutLineSpec(
          new LayoutPoint2d(x, layout.Origin.Y),
          new LayoutPoint2d(x, layout.Origin.Y + layout.Height),
          unchecked((int)0xFF808080),
          0.1,
          false));
      }

      for (int r = 0; r <= layout.RowCount; r++)
      {
        double y = layout.Origin.Y + r * cellH;
        AddLine(geometry, attributes, new LayoutLineSpec(
          new LayoutPoint2d(layout.Origin.X, y),
          new LayoutPoint2d(layout.Origin.X + layout.Width, y),
          unchecked((int)0xFF808080),
          0.1,
          false));
      }

      foreach (var cell in layout.Cells)
      {
        double x = layout.Origin.X + cell.Column * cellW + 0.5;
        double y = layout.Origin.Y + (layout.RowCount - cell.Row - 1) * cellH + cellH * 0.25;
        AddText(geometry, attributes, new LayoutTextSpec(
          new LayoutPoint2d(x, y),
          cell.Text,
          cell.IsHeader ? unchecked((int)0xFF000000) : unchecked((int)0xFF202020),
          2.0,
          false));
      }

      string anchor = FormatAnchor(insertionPoint);
      var tags = new Dictionary<string, string>
      {
        [PresentationBlockTags.BlockKind] = PresentationBlockTags.KindTable,
        [PresentationBlockTags.ObjectIds] = layout.ObjectIdsCsv,
        [PresentationBlockTags.Mode] = modeName ?? string.Empty,
        [PresentationBlockTags.AnchorFrame] = anchor
      };

      return Bake(doc, geometry, attributes, blockName, insertionPoint, tags);
    }

    private static PresentationBlockResult Bake(
      RhinoDoc doc,
      List<GeometryBase> geometry,
      List<ObjectAttributes> attributes,
      string blockName,
      Point3d insertionPoint,
      Dictionary<string, string> tags)
    {
      if (geometry.Count == 0)
        throw new InvalidOperationException("No geometry to bake.");

      string name = EnsureUniqueBlockName(doc, blockName);
      int idef = doc.InstanceDefinitions.Add(name, "Progesi AxisVar presentation", Point3d.Origin, geometry, attributes);
      if (idef < 0)
        throw new InvalidOperationException("Failed to create instance definition.");

      var xform = Transform.Translation(insertionPoint.X, insertionPoint.Y, insertionPoint.Z);
      Guid instanceId = doc.Objects.AddInstanceObject(idef, xform);
      var instance = doc.Objects.FindId(instanceId);
      if (instance == null)
        throw new InvalidOperationException("Failed to insert block instance.");

      foreach (var kv in tags)
        instance.Attributes.SetUserString(kv.Key, kv.Value);

      instance.CommitChanges();

      var idefObj = doc.InstanceDefinitions[idef];
      return new PresentationBlockResult(instanceId, name, idefObj != null ? idefObj.Id : Guid.Empty);
    }

    private static void AddLine(List<GeometryBase> geometry, List<ObjectAttributes> attributes, LayoutLineSpec line)
    {
      var ptA = ToPoint3d(line.Start);
      var ptB = ToPoint3d(line.End);
      geometry.Add(new LineCurve(ptA, ptB));
      attributes.Add(CreateAttributes(line.ColorArgb));
    }

    private static void AddPolyline(List<GeometryBase> geometry, List<ObjectAttributes> attributes, LayoutPolylineSpec pl)
    {
      if (pl.Points == null || pl.Points.Count < 2)
        return;

      var pts = new Point3d[pl.Points.Count];
      for (int i = 0; i < pl.Points.Count; i++)
        pts[i] = ToPoint3d(pl.Points[i]);

      geometry.Add(new PolylineCurve(pts));
      attributes.Add(CreateAttributes(pl.ColorArgb));
    }

    private static void AddText(List<GeometryBase> geometry, List<ObjectAttributes> attributes, LayoutTextSpec text)
    {
      if (string.IsNullOrEmpty(text.Text))
        return;

      var plane = new Plane(
        new Point3d(text.Position.X, text.Position.Y, TextPlaneOffsetZ),
        Vector3d.XAxis,
        Vector3d.YAxis);

      var entity = new TextEntity
      {
        Plane = plane,
        PlainText = text.Text,
        TextHeight = text.HeightMm,
        Justification = TextJustification.BottomLeft
      };

      geometry.Add(entity);
      attributes.Add(CreateAttributes(text.ColorArgb));
    }

    private static ObjectAttributes CreateAttributes(int colorArgb)
    {
      var attr = new ObjectAttributes();
      attr.ColorSource = ObjectColorSource.ColorFromObject;
      attr.ObjectColor = System.Drawing.Color.FromArgb(colorArgb);
      return attr;
    }

    private static Point3d ToPoint3d(LayoutPoint2d p)
      => new Point3d(p.X, p.Y, TextPlaneOffsetZ);

    private static string FormatAnchor(Point3d insertion)
      => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R}",
        insertion.X,
        insertion.Y,
        insertion.Z);

    private static string EnsureUniqueBlockName(RhinoDoc doc, string blockName)
    {
      string baseName = string.IsNullOrWhiteSpace(blockName) ? "Progesi.AxisVar.Diagram" : blockName.Trim();
      string name = baseName;
      int suffix = 1;
      while (doc.InstanceDefinitions.Find(name) != null)
      {
        name = baseName + "." + suffix.ToString(CultureInfo.InvariantCulture);
        suffix++;
      }
      return name;
    }
  }
}
