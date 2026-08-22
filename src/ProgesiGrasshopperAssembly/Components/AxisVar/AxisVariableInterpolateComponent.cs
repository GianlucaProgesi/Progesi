#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableInterpolateComponent : AxisVarConsumerComponentBase
  {
    public AxisVariableInterpolateComponent()
      : base("AxisVariable.Interpolate", "AxEval",
        "Evaluate the axis value curve at a station (numeric-gated interpolation).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("7d980066-092d-47ea-8b89-ead614a58bd8");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle (optional when Id is set).", GH_ParamAccess.item);
      p.AddNumberParameter("Station", "S", "Station (real or normalized).", GH_ParamAccess.item);
      p.AddBooleanParameter("Normalized", "Nrm", "True when Station is normalized [0,1].", GH_ParamAccess.item, true);
      p.AddIntegerParameter("Mode", "M", "Optional CurveParameterMapper mode override (default = axis mode).", GH_ParamAccess.item, -1);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[4].Optional = true;
      Params.Input[5].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Value", "V", "Interpolated or step value.", GH_ParamAccess.item);
      p.AddNumberParameter("StationNorm", "Sn", "Station normalized.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Diagnostics.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;

      if (!AxisVarGhSupport.TryLoadAxis(da, 1, this, repo, out var axis, optionalIdInputIndex: 5))
        return;

      double station = 0;
      if (!da.GetData(2, ref station))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Station is required.");
        return;
      }

      bool normalized = true;
      da.GetData(3, ref normalized);

      int modeInt = -1;
      da.GetData(4, ref modeInt);

      try
      {
        if (!AxisVarGhSupport.TryDecodeCurve(axis, this, out var curve) || curve == null)
          return;

        var mode = modeInt >= 0 ? AxisVarGhSupport.ParseMode(modeInt) : axis.Mode;
        var mapper = AxisVarGhSupport.CreateMapper(curve, mode);
        double norm = normalized ? station : mapper.RealToNormalized(station);

        var (value, info) = AxisVarGhSupport.EvaluateInterpolateValue(axis, norm);

        da.SetData(0, value);
        da.SetData(1, Math.Max(0.0, Math.Min(1.0, norm)));
        da.SetData(2, info);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
