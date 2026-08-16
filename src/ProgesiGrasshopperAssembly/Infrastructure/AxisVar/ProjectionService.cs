using System;
using System.Collections.Generic;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>
  /// Projects a 3D axis curve to plan (XY) and station–elevation profile views.
  /// Profile abscissa uses horizontal (plan-projected) arc-length; ordinate is elevation (Z).
  /// </summary>
  public static class ProjectionService
  {
    /// <summary>Length/parameter tolerance shared with <see cref="CurveParameterMapper"/>.</summary>
    public const double DefaultTolerance = ProgesiAxisVariable.DefaultTolerance;

    public static Curve ProjectToPlanXY(Curve curve3d)
    {
      if (curve3d == null) throw new ArgumentNullException(nameof(curve3d));
      var projected = curve3d.DuplicateCurve();
      if (!projected.Transform(Transform.PlanarProjection(Plane.WorldXY)))
        throw new InvalidOperationException("Planar projection to WorldXY failed.");
      return projected;
    }

    /// <summary>
    /// Builds a 2D profile curve: X = plan-projected arc-length (station), Y = elevation (Z).
    /// </summary>
    public static Curve BuildProfileCurve(Curve curve3d, double tolerance = DefaultTolerance)
    {
      if (curve3d == null) throw new ArgumentNullException(nameof(curve3d));

      var plan = ProjectToPlanXY(curve3d);
      double totalPlan = plan.GetLength();
      if (totalPlan <= tolerance)
        throw new InvalidOperationException("Plan-projected curve length is zero.");

      int count = Math.Max(2, Math.Min(512, (int)Math.Ceiling(totalPlan / Math.Max(tolerance * 10.0, 0.5)) + 1));
      var pts = new List<Point3d>(count);
      var domain = curve3d.Domain;
      for (int i = 0; i < count; i++)
      {
        double frac = count == 1 ? 0.0 : i / (double)(count - 1);
        double t = domain.ParameterAt(frac);
        var p3 = curve3d.PointAt(t);
        plan.ClosestPoint(new Point3d(p3.X, p3.Y, 0.0), out double planT);
        double station = plan.GetLength(new Interval(plan.Domain.T0, planT));
        pts.Add(new Point3d(station, p3.Z, 0.0));
      }

      var cleaned = new List<Point3d>();
      foreach (var p in pts)
      {
        if (cleaned.Count == 0 || p.X > cleaned[cleaned.Count - 1].X + tolerance)
          cleaned.Add(p);
      }

      if (cleaned.Count < 2)
      {
        var last = pts[pts.Count - 1];
        cleaned.Add(new Point3d(totalPlan, last.Y, 0.0));
      }

      var profile = Curve.CreateInterpolatedCurve(cleaned, 3, CurveKnotStyle.Chord);
      if (profile == null)
        throw new InvalidOperationException("Failed to build profile curve.");
      return profile;
    }
  }
}
