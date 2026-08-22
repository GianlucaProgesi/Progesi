#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableDiagramComponent : GH_Component
  {
    private const int AxesInputIndex = 1;
    private const int IdsInputIndex = 2;
    private const int SettingsInputIndex = 4;
    private const int ColoursInputIndex = 5;

    public AxisVariableDiagramComponent()
      : base("AxisVariable.Diagram", "AxDiag",
        "Bake a 2D value-vs-station diagram block for one or more axes.",
        "Progesi", "Diagrams & Tables")
    { }

    public override Guid ComponentGuid => new Guid("7f2a9d41-6c85-4b3e-9e12-8a4f0d6b2c57");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axes", "Ax", "Axis handles.", GH_ParamAccess.list);
      p.AddIntegerParameter("Ids", "Id", "Persisted axis Ids.", GH_ParamAccess.list);
      p.AddPointParameter("Point", "P", "World-XY insertion point.", GH_ParamAccess.item);
      p.AddGenericParameter("Settings", "S", "DiagramSettings (optional).", GH_ParamAccess.item);
      p.AddColourParameter("Colours", "C", "Series colours (optional).", GH_ParamAccess.list);

      p[AxesInputIndex].Optional = true;
      p[IdsInputIndex].Optional = true;
      p[SettingsInputIndex].Optional = true;
      p[ColoursInputIndex].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("BlockId", "Bi", "Baked block instance Id.", GH_ParamAccess.item);
      p.AddTextParameter("BlockName", "Bn", "Baked block name.", GH_ParamAccess.item);
      p.AddNumberParameter("ScaleX", "Sx", "Abscissa scale (mm per station unit).", GH_ParamAccess.item);
      p.AddNumberParameter("ScaleY", "Sy", "Ordinate scale (mm per value unit).", GH_ParamAccess.item);
    }

    protected override void BeforeSolveInstance()
    {
      AxisVarPresentationGhSupport.PrepareOptionalListInput(this, AxesInputIndex);
      AxisVarPresentationGhSupport.PrepareOptionalListInput(this, IdsInputIndex);
      AxisVarPresentationGhSupport.PrepareOptionalListInput(this, ColoursInputIndex);
      AxisVarGhSupport.PrepareOptionalAxisInput(this, SettingsInputIndex);
      base.BeforeSolveInstance();
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;

      if (!AxisVarPresentationGhSupport.TryLoadAxes(da, AxesInputIndex, IdsInputIndex, this, repo, out var axes))
        return;

      Point3d point = Point3d.Origin;
      if (!da.GetData(3, ref point))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Point is required.");
        return;
      }

      object? settingsInput = null;
      da.GetData(SettingsInputIndex, ref settingsInput);
      var settings = AxisVarPresentationGhSupport.ResolveDiagramSettings(settingsInput);
      var colours = AxisVarPresentationGhSupport.ReadOptionalColours(da, ColoursInputIndex);
      AxisVarPresentationGhSupport.ApplySeriesColours(settings, colours);

      try
      {
        var result = AxisVarPresentationGhSupport.BakeDiagram(doc, axes, settings, point, this);
        da.SetData(0, result.Block.InstanceId.ToString());
        da.SetData(1, result.Block.BlockName);
        da.SetData(2, result.Scale.ScaleX);
        da.SetData(3, result.Scale.ScaleY);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
