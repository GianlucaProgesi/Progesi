#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableValueCurveDefineComponent : GH_Component
  {
    public AxisVariableValueCurveDefineComponent()
      : base("AxisVariable.ValueCurve.Define", "AxVC",
        "Define the axis value curve (constant, linear, or drawn NURBS).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("3cc7099d-64e5-478d-9c62-70674b39524f");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle or Id.", GH_ParamAccess.item);
      p.AddIntegerParameter("Kind", "K", "0=Constant, 1=Linear, 2=DrawnCurve", GH_ParamAccess.item, 0);
      p.AddNumberParameter("Constant", "C", "Constant value (Kind=0).", GH_ParamAccess.item, 0.0);
      p.AddNumberParameter("LinearStart", "Y0", "Linear start value (Kind=1).", GH_ParamAccess.item, 0.0);
      p.AddNumberParameter("LinearEnd", "Y1", "Linear end value (Kind=1).", GH_ParamAccess.item, 1.0);
      p.AddCurveParameter("Curve", "Cv", "Drawn value curve x→value (Kind=2).", GH_ParamAccess.item);
      Params.Input[6].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Axis", "Ax", "Updated axis handle.", GH_ParamAccess.item);
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

      if (!AxisVarGhSupport.TryLoadAxis(da, 1, this, repo, out var axis))
        return;

      int kind = 0;
      da.GetData(2, ref kind);
      double constant = 0;
      da.GetData(3, ref constant);
      double y0 = 0;
      da.GetData(4, ref y0);
      double y1 = 1;
      da.GetData(5, ref y1);
      Curve? drawn = null;
      da.GetData(6, ref drawn);

      try
      {
        var edited = AxisVarGhSupport.CloneForEdit(axis);
        ProgesiFunctionSegment segment;

        switch (kind)
        {
          case 0:
            segment = new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Constant, constantValue: constant);
            break;
          case 1:
            segment = new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs,
              nurbs: new ProgesiNurbsPayload(1,
                new[] { (0.0, y0), (1.0, y1) },
                new[] { 1.0, 1.0 },
                new[] { 0.0, 0.0, 1.0, 1.0 }));
            break;
          case 2:
            if (drawn == null)
            {
              AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Curve is required for Kind=2 (DrawnCurve).");
              return;
            }
            var payload = ProgesiNurbsValueCurveCodec.FromCurve(drawn);
            segment = new ProgesiFunctionSegment(0.0, 1.0, ProgesiFunctionSegmentKind.Nurbs, nurbs: payload);
            break;
          default:
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Kind must be 0, 1, or 2.");
            return;
        }

        var fn = new ProgesiFunction(edited.Id, edited.Name + "-vc", new[] { segment });
        edited.SetFunctionRef(ProgesiFunctionRef.Embed(fn));

        var handle = AxisVarGhSupport.SaveAxis(repo, edited);
        da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
        da.SetData(1, handle.Axis.ContentHash);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
