#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableByEqualSegmentsComponent : AxisVarConsumerComponentBase
  {
    public AxisVariableByEqualSegmentsComponent()
      : base("AxisVariable.ByEqualSegments", "AxByEq",
        "Divide the axis into N equal normalized segments (endpoints included).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("7e040d5d-80e0-420a-a7e7-33ab4ffce259");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle (optional when Id is set).", GH_ParamAccess.item);
      p.AddIntegerParameter("Count", "N", "Segments (N → N+1 stations, incl. both ends).", GH_ParamAccess.item, 5);
      p.AddGenericParameter("Values", "V", "Optional values at stations (typed per axis ValueTypeKey).", GH_ParamAccess.list);
      p.AddTextParameter("Labels", "Lb", "Optional station names (not values).", GH_ParamAccess.list);
      p.AddBooleanParameter("Replace", "Rpl", "When true, reset stations instead of additive merge.", GH_ParamAccess.item, false);
      p.AddIntegerParameter("Mode", "M", "Optional CurveParameterMapper mode override (default = axis mode).", GH_ParamAccess.item, -1);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[3].Optional = true;
      Params.Input[4].Optional = true;
      Params.Input[5].Optional = true;
      Params.Input[6].Optional = true;
      Params.Input[7].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Axis", "Ax", "Updated axis handle.", GH_ParamAccess.item);
      p.AddNumberParameter("Stations", "S", "Normalized stations.", GH_ParamAccess.list);
      p.AddNumberParameter("RealStations", "Rs", "Real stations.", GH_ParamAccess.list);
      p.AddTextParameter("Hash", "H", "Content hash.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;
      var varRepo = new RhinoVariableRepository(doc);

      int count = 5;
      da.GetData(2, ref count);

      var values = AxisVarGhSupport.ReadOptionalValueLabels(da, 3);
      var labels = AxisVarGhSupport.ReadOptionalLabels(da, 4);
      bool replace = false;
      da.GetData(5, ref replace);
      int modeInt = -1;
      da.GetData(6, ref modeInt);

      if (!AxisVarGhSupport.TryApplyVariation(
            da, 1, this, repo, varRepo,
            new ByEqualSegmentsStrategy(count),
            out var handle, out var normalized, out var real,
            values,
            labels,
            replace: replace,
            modeOverrideInt: modeInt >= 0 ? (int?)modeInt : null,
            optionalIdInputIndex: 7))
        return;

      da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
      da.SetDataList(1, normalized);
      da.SetDataList(2, real);
      da.SetData(3, handle!.Axis.ContentHash);
    }
  }
}
