#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableTableComponent : GH_Component
  {
    private const int AxesInputIndex = 1;
    private const int IdsInputIndex = 2;
    private const int SettingsInputIndex = 4;

    public AxisVariableTableComponent()
      : base("AxisVariable.Table", "AxTbl",
        "Bake a station table block for one or more axes.",
        "Progesi", "Diagrams & Tables")
    { }

    public override Guid ComponentGuid => new Guid("e6a1c4f8-2b93-4d7e-8f05-3c9b7e2a1d46");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axes", "Ax", "Axis handles.", GH_ParamAccess.list);
      p.AddIntegerParameter("Ids", "Id", "Persisted axis Ids.", GH_ParamAccess.list);
      p.AddPointParameter("Point", "P", "World-XY insertion point.", GH_ParamAccess.item);
      p.AddGenericParameter("Settings", "S", "TableSettings (optional).", GH_ParamAccess.item);

      p[AxesInputIndex].Optional = true;
      p[IdsInputIndex].Optional = true;
      p[SettingsInputIndex].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("BlockId", "Bi", "Baked block instance Id.", GH_ParamAccess.item);
      p.AddTextParameter("BlockName", "Bn", "Baked block name.", GH_ParamAccess.item);
    }

    protected override void BeforeSolveInstance()
    {
      AxisVarPresentationGhSupport.PrepareOptionalListInput(this, AxesInputIndex);
      AxisVarPresentationGhSupport.PrepareOptionalListInput(this, IdsInputIndex);
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
      var (settings, defaultUnit) = AxisVarPresentationGhSupport.ResolveTableSettingsWithUnit(settingsInput);

      try
      {
        var result = AxisVarPresentationGhSupport.BakeTable(doc, axes, settings, point, this, defaultUnit);
        da.SetData(0, result.InstanceId.ToString());
        da.SetData(1, result.BlockName);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
