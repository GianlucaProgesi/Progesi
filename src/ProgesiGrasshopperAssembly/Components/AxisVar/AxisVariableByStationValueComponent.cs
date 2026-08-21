#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableByStationValueComponent : AxisVarConsumerComponentBase
  {
    public AxisVariableByStationValueComponent()
      : base("AxisVariable.ByStationValue", "AxBySV",
        "Place stations at explicit real arc-length positions (optional values/variable ids).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("9e8c368e-cf80-4268-acce-7dc5f66eb8e5");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle (optional when Id is set).", GH_ParamAccess.item);
      p.AddNumberParameter("RealStations", "Rs", "Real arc-length stations.", GH_ParamAccess.list);
      p.AddGenericParameter("Values", "V", "Optional values (numeric or string labels at stations).", GH_ParamAccess.list);
      p.AddIntegerParameter("VariableIds", "Vid", "Optional variable ids (1:1 with stations).", GH_ParamAccess.list);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[3].Optional = true;
      Params.Input[4].Optional = true;
      Params.Input[5].Optional = true;
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

      var realStations = new List<double>();
      if (!da.GetDataList(2, realStations) || realStations.Count == 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "RealStations is required.");
        return;
      }

      var values = AxisVarGhSupport.ReadOptionalValueLabels(da, 3);
      var variableIds = new List<int>();
      da.GetDataList(4, variableIds);

      if (!AxisVarGhSupport.TryApplyVariation(
            da, 1, this, repo,
            new ByStationValueStrategy(realStations),
            out var handle, out var normalized, out var real,
            values,
            variableIds.Count > 0 ? variableIds : null,
            optionalIdInputIndex: 5))
        return;

      da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
      da.SetDataList(1, normalized);
      da.SetDataList(2, real);
      da.SetData(3, handle!.Axis.ContentHash);
    }
  }
}
