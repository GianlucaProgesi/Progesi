using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

public sealed class AxisVarDefineComponent : GH_Component
{
  public AxisVarDefineComponent()
    : base("AxisVar.Define", "AxisDef",
      "Create an AxisContext from a Rhino curve.",
      "Progesi", "AxisVar")
  { }

  protected override void RegisterInputParams(GH_InputParamManager pManager)
  {
    pManager.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
    pManager.AddCurveParameter("Curve", "C", "Axis curve (3D).", GH_ParamAccess.item);
    pManager.AddTextParameter("AxisName", "N", "Axis name (optional).", GH_ParamAccess.item, "AXIS");
    pManager.AddIntegerParameter("Mode", "M", "0=Curve3d, 1=PlanXY, 2=Profile", GH_ParamAccess.item, 0);
  }

  protected override void RegisterOutputParams(GH_OutputParamManager pManager)
  {
    pManager.AddGenericParameter("AxisContext", "Ctx", "Axis context wrapper.", GH_ParamAccess.item);
  }
   protected override void SolveInstance(IGH_DataAccess DA)
  {
    bool run = false;
    if (!DA.GetData(0, ref run) || !run)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run=True to execute.");
      return;
    }

    Curve crv = null;
    if (!DA.GetData(1, ref crv) || crv == null) return;

    string axisName = "AXIS";
    DA.GetData(2, ref axisName);

    int modeInt = 0;
    DA.GetData(3, ref modeInt);

    var mode = (AxisCurveMode)Math.Max(0, Math.Min(2, modeInt));
    var ctx = new AxisContext(Guid.NewGuid(), axisName ?? "AXIS", crv, mode);

    DA.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(ctx));
  }


  public override Guid ComponentGuid => new Guid("B0D7F3D1-7D5B-4C5E-9AF2-7A0D58A73A11");

}
