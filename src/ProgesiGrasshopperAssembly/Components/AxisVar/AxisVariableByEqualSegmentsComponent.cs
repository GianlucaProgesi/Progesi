#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableByEqualSegmentsComponent : GH_Component
  {
    public AxisVariableByEqualSegmentsComponent()
      : base("AxisVariable.ByEqualSegments", "AxByEq",
        "Divide the axis into N equal normalized segments (endpoints included).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("e1f2a3b4-5263-789a-bcde-f01234567890");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle or Id.", GH_ParamAccess.item);
      p.AddIntegerParameter("Count", "N", "Number of divisions (>= 2).", GH_ParamAccess.item, 5);
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

      int count = 5;
      da.GetData(2, ref count);

      if (!AxisVarGhSupport.TryApplyVariation(
            da, 1, this, repo,
            new ByEqualSegmentsStrategy(count),
            out var handle, out var normalized, out var real))
        return;

      da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
      da.SetDataList(1, normalized);
      da.SetDataList(2, real);
      da.SetData(3, handle!.Axis.ContentHash);
    }
  }
}
