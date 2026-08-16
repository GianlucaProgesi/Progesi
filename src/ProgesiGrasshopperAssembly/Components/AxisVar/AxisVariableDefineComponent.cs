#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableDefineComponent : GH_Component
  {
    public AxisVariableDefineComponent()
      : base("AxisVariable.Define", "AxDef",
        "Define and persist a ProgesiAxisVariable from a 3D axis curve.",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("9bf1e56b-2937-4463-8c77-8d49d761a067");
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddCurveParameter("Curve", "C", "3D axis curve.", GH_ParamAccess.item);
      p.AddTextParameter("AxisName", "Ax", "Axis label.", GH_ParamAccess.item, "AXIS");
      p.AddIntegerParameter("Mode", "M", "0=Curve3d, 1=PlanXY, 2=Profile", GH_ParamAccess.item, 0);
      p.AddTextParameter("Name", "Nm", "Series name (unique per axis object).", GH_ParamAccess.item, "Thickness");
      p.AddTextParameter("ValueTypeKey", "T", "Value type key.", GH_ParamAccess.item, "System.Double");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Axis", "Ax", "Persisted axis handle.", GH_ParamAccess.item);
      p.AddIntegerParameter("Id", "Id", "Axis id.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "H", "Content hash.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      Curve? curve = null;
      if (!da.GetData(1, ref curve) || curve == null)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Curve is required.");
        return;
      }

      string axisName = "AXIS";
      da.GetData(2, ref axisName);
      int modeInt = 0;
      da.GetData(3, ref modeInt);
      string name = "Thickness";
      da.GetData(4, ref name);
      string valueTypeKey = "System.Double";
      da.GetData(5, ref valueTypeKey);

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;

      try
      {
        var mode = AxisVarGhSupport.ParseMode(modeInt);
        var mapper = AxisVarGhSupport.CreateMapper(curve, mode);
        var payload = ProgesiGeometryValueCodec.Encode(curve);
        int id = AxisVarGhSupport.NextAxisId(repo);

        var axis = new ProgesiAxisVariable(
          id,
          axisName ?? "AXIS",
          name ?? "Thickness",
          valueTypeKey ?? "System.Double",
          mapper.TotalLength,
          curvePayload: payload,
          mode: mode,
          keyPoints: new[] { 0.0, 1.0 });

        var handle = AxisVarGhSupport.SaveAxis(repo, axis);
        da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
        da.SetData(1, handle.AxisId);
        da.SetData(2, handle.Axis.ContentHash);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
