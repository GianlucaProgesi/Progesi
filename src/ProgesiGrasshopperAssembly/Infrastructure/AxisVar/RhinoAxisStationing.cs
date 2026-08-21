using System;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>
  /// Legacy linear station helpers for deprecated AxisVar components.
  /// Prefer <see cref="CurveParameterMapper"/> for true arc-length stationing.
  /// </summary>
  [Obsolete("Use CurveParameterMapper for true arc-length stationing.")]
  public static class RhinoAxisStationing
  {
    public static double GetAxisLength(AxisContext ctx)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      var mode = (ProgesiCore.AxisCurveMode)(int)ctx.Mode;
      return new CurveParameterMapper(ctx.Curve3d, mode).TotalLength;
    }

    public static double ToNormalized(AxisContext ctx, double stationReal)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      var mode = (ProgesiCore.AxisCurveMode)(int)ctx.Mode;
      return new CurveParameterMapper(ctx.Curve3d, mode).RealToNormalized(stationReal);
    }

    public static double ToReal(AxisContext ctx, double stationNormalized)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      var mode = (ProgesiCore.AxisCurveMode)(int)ctx.Mode;
      return new CurveParameterMapper(ctx.Curve3d, mode).NormalizedToReal(stationNormalized);
    }
  }
}
