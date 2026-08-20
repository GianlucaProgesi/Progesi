#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;

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
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[4].Optional = true;
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

      if (!AxisVarGhSupport.TryLoadAxis(da, 1, this, repo, out var axis, optionalIdInputIndex: 4))
        return;

      double station = 0;
      if (!da.GetData(2, ref station))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Station is required.");
        return;
      }

      bool normalized = true;
      da.GetData(3, ref normalized);

      try
      {
        if (!AxisVarGhSupport.TryDecodeCurve(axis, this, out var curve) || curve == null)
          return;

        var mapper = AxisVarGhSupport.CreateMapper(curve, axis.Mode);
        double norm = normalized ? station : mapper.RealToNormalized(station);
        norm = Math.Max(0.0, Math.Min(1.0, norm));

        object? value;
        string info;

        if (axis.FunctionRef.IsEmpty)
        {
          value = AxisVarGhSupport.EvaluateStepValue(axis, norm);
          info = "No value curve defined on axis; step value from nearest keypoint label.";
        }
        else if (!string.Equals(axis.ValueTypeKey, "System.Double", StringComparison.Ordinal))
        {
          value = AxisVarGhSupport.EvaluateStepValue(axis, norm);
          info = "Non-numeric ValueTypeKey: step value at nearest keypoint.";
        }
        else
        {
          if (axis.FunctionRef.Embedded == null)
          {
            value = null;
            info = "Function reference is not embedded; cannot evaluate.";
          }
          else
          {
            var vc = new ProgesiValueCurve(axis.FunctionRef.Embedded);
            value = vc.Evaluate(norm);
            info = "Interpolated via ProgesiValueCurve.Evaluate.";
          }
        }

        da.SetData(0, value);
        da.SetData(1, norm);
        da.SetData(2, info);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
