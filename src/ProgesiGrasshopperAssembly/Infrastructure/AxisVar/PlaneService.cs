using System;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>
  /// Builds perpendicular reference planes at axis stations (3D UVW and true-vertical variants).
  /// </summary>
  public static class PlaneService
  {
    public const double DefaultTolerance = CurveParameterMapper.LengthTolerance;

    public static Plane FrameAtParameter(Curve curve, double t, bool trueVertical = false)
    {
      if (curve == null) throw new ArgumentNullException(nameof(curve));
      if (!curve.PerpendicularFrameAt(t, out Plane frame))
        throw new InvalidOperationException("Cannot build perpendicular frame at parameter.");

      if (!trueVertical)
        return frame;

      var origin = curve.PointAt(t);
      var tangent = curve.TangentAt(t);
      tangent.Z = 0.0;
      if (tangent.Length <= DefaultTolerance)
        tangent = Vector3d.XAxis;
      tangent.Unitize();

      var z = Vector3d.ZAxis;
      var y = Vector3d.CrossProduct(z, tangent);
      if (y.Length <= DefaultTolerance)
        y = Vector3d.YAxis;
      y.Unitize();
      var x = Vector3d.CrossProduct(y, z);
      x.Unitize();
      return new Plane(origin, x, y);
    }

    public static Plane FrameAtNormalized(CurveParameterMapper mapper, double normalized, bool trueVertical = false)
    {
      if (mapper == null) throw new ArgumentNullException(nameof(mapper));
      if (!mapper.TryNormalizedToParameter(normalized, out double t))
        throw new ArgumentOutOfRangeException(nameof(normalized), "Normalized station is out of range.");
      return FrameAtParameter(mapper.SourceCurve, t, trueVertical);
    }

    public static Plane TrueVerticalAtNormalized(CurveParameterMapper mapper, double normalized)
      => FrameAtNormalized(mapper, normalized, trueVertical: true);
  }
}
