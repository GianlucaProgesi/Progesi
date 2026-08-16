#nullable enable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableInterpolateComponent : GH_Component
  {
    public AxisVariableInterpolateComponent()
      : base("AxisVariable.Interpolate", "AxEval",
        "Evaluate the axis value curve at a station (numeric-gated interpolation).",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("b8c9d0e1-2f30-4567-89ab-cdef01234567");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle or Id.", GH_ParamAccess.item);
      p.AddNumberParameter("Station", "S", "Station (real or normalized).", GH_ParamAccess.item);
      p.AddBooleanParameter("Normalized", "Nrm", "True when Station is normalized [0,1].", GH_ParamAccess.item, true);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddNumberParameter("Value", "V", "Interpolated value (null if undefined).", GH_ParamAccess.item);
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

      if (!AxisVarGhSupport.TryLoadAxis(da, 1, this, repo, out var axis))
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

        double? value = null;
        string info;

        if (axis.FunctionRef.IsEmpty)
        {
          info = "No value curve defined on axis.";
        }
        else if (!string.Equals(axis.ValueTypeKey, "System.Double", StringComparison.Ordinal))
        {
          var vc = axis.FunctionRef.Embedded != null
            ? new ProgesiValueCurve(axis.FunctionRef.Embedded)
            : null;
          value = vc?.Evaluate(FloorToStep(norm, axis.KeyPoints));
          info = "Non-numeric ValueTypeKey: step/constant evaluation at nearest keypoint.";
        }
        else
        {
          if (axis.FunctionRef.Embedded == null)
          {
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

    private static double FloorToStep(double norm, IReadOnlyList<double> keyPoints)
    {
      if (keyPoints == null || keyPoints.Count == 0)
        return norm;
      double best = keyPoints[0];
      foreach (var kp in keyPoints)
      {
        if (kp <= norm + ProgesiAxisVariable.DefaultTolerance)
          best = kp;
        else
          break;
      }
      return best;
    }
  }
}
