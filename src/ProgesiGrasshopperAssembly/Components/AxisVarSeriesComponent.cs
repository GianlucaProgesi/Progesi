using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

public sealed class AxisVarSeriesComponent : GH_Component
{
  public AxisVarSeriesComponent()
    : base("AxisVar.Series", "AxisSeries",
      "Create an AxisVarMapping (normalized stations + variable hashes).",
      "Progesi", "AxisVar")
  { }

  protected override void RegisterInputParams(GH_InputParamManager pManager)
  {
    pManager.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
    pManager.AddGenericParameter("AxisContext", "Ctx", "Axis context from AxisVar.Define.", GH_ParamAccess.item);
    pManager.AddTextParameter("Name", "Nm", "Series name (all variables must share this name).", GH_ParamAccess.item);
    pManager.AddTextParameter("ValueTypeKey", "T", "Value type key (e.g. System.Double).", GH_ParamAccess.item, "System.Double");
    pManager.AddNumberParameter("Stations", "S", "Stations (real or normalized).", GH_ParamAccess.list);
    pManager.AddBooleanParameter("StationsAreNormalized", "Nrm", "True if stations are normalized [0..1].", GH_ParamAccess.item, true);
    pManager.AddTextParameter("VariableHashes", "H", "Variable hashes (1:1 with stations).", GH_ParamAccess.list);
  }

  protected override void RegisterOutputParams(GH_OutputParamManager pManager)
  {
    pManager.AddGenericParameter("AxisVarMapping", "Map", "Mapping transport (stationsNormalized + hashes).", GH_ParamAccess.item);
  }
  protected override void SolveInstance(IGH_DataAccess DA)
  {
     bool run = false;
    if (!DA.GetData(0, ref run) || !run)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run=True to execute.");
      return;
    }

    object ctxObj = null;
    if (!DA.GetData(1, ref ctxObj) || ctxObj == null)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Ctx is required (connect AxisDef output).");
      return;
    }

    // UNWRAP: GH spesso passa un wrapper, non l'oggetto nudo
    if (ctxObj is GH_ObjectWrapper ow && ow.Value != null)
      ctxObj = ow.Value;
    else if (ctxObj is IGH_Goo goo)
      ctxObj = goo.ScriptVariable();

    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      $"Ctx runtime: {ctxObj?.GetType().FullName} @ {ctxObj?.GetType().Assembly.GetName().Name}");

    if (!(ctxObj is AxisContext ctx))
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input Ctx is not an AxisContext.");
      return;
    }

    string name = null;
    if (!DA.GetData(2, ref name) || string.IsNullOrWhiteSpace(name))
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Nm (series name) is required.");
      return;
    }

    string valueTypeKey = "System.Double";
    DA.GetData(3, ref valueTypeKey);

    var stations = new List<double>();
    if (!DA.GetDataList(4, stations) || stations.Count == 0)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "S (stations) is required (provide at least 1 value).");
      return;
    }

    bool stationsAreNormalized = true;
    DA.GetData(5, ref stationsAreNormalized);

    var hashes = new List<string>();
    if (!DA.GetDataList(6, hashes) || hashes.Count == 0)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "H (hashes) is required (1:1 with stations).");
      return;
    }

    var stationsNorm = new List<double>(stations.Count);
    if (stationsAreNormalized)
      stationsNorm.AddRange(stations);
    else
      foreach (var sReal in stations)
        stationsNorm.Add(RhinoAxisStationing.ToNormalized(ctx, sReal));

    try
    {
      var map = new AxisVarMapping(ctx.AxisGuid, ctx.AxisName, name, valueTypeKey, stationsNorm, hashes);
      DA.SetData(0, map);
    }
    catch (Exception ex)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.ToString());
    }
  }

  public override Guid ComponentGuid => new Guid("0E6C3D9C-6E2B-4C3A-9E1B-4E8A6E61E2D4");

}
