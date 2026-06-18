using System;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  public static class RhinoAxisStationing
  {
    public static double GetAxisLength(AxisContext ctx)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      return ctx.Curve3d.GetLength();
    }

    public static double ToNormalized(AxisContext ctx, double stationReal)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      var L = ctx.Curve3d.GetLength();
      if (L <= 0) throw new InvalidOperationException("Curve length is zero.");
      return stationReal / L;
    }

    public static double ToReal(AxisContext ctx, double stationNormalized)
    {
      if (ctx == null) throw new ArgumentNullException(nameof(ctx));
      var L = ctx.Curve3d.GetLength();
      if (L <= 0) throw new InvalidOperationException("Curve length is zero.");
      return stationNormalized * L;
    }
  }
}
