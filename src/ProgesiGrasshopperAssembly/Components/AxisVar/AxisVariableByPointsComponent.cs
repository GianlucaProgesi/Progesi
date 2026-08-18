#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableByPointsComponent : GH_Component
  {
    public AxisVariableByPointsComponent()
      : base("AxisVariable.ByPoints", "AxByPt",
        "Project 3D points to the nearest station on the axis.",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("e5ded3ea-71bd-4296-8318-e36392a64937");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle (optional when Id is set).", GH_ParamAccess.item);
      p.AddPointParameter("Points", "P", "3D points to project.", GH_ParamAccess.list);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[3].Optional = true;
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

      var points = new List<Point3d>();
      if (!da.GetDataList(2, points) || points.Count == 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Points is required.");
        return;
      }

      if (!AxisVarGhSupport.TryApplyVariation(
            da, 1, this, repo,
            new ByPointsStrategy(points),
            out var handle, out var normalized, out var real,
            optionalIdInputIndex: 3))
        return;

      da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
      da.SetDataList(1, normalized);
      da.SetDataList(2, real);
      da.SetData(3, handle!.Axis.ContentHash);
    }
  }
}
