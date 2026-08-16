using System;
using System.Collections.Generic;
using System.Linq;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiRepositories.Rhino
{
  /// <summary>
  /// Converts between Core <see cref="ProgesiNurbsPayload"/> and Rhino <see cref="NurbsCurve"/>.
  /// </summary>
  public static class ProgesiNurbsValueCurveCodec
  {
    public const double MonotonicTolerance = ProgesiAxisVariable.DefaultTolerance;

    public static NurbsCurve ToNurbsCurve(ProgesiNurbsPayload payload)
    {
      if (payload == null) throw new ArgumentNullException(nameof(payload));

      var points = payload.ControlPoints
        .Select(cp => new Point3d(cp.X, cp.Value, 0.0))
        .ToList();

      int order = payload.Degree + 1;
      var nurbs = new NurbsCurve(3, true, order, points.Count);
      for (int i = 0; i < points.Count; i++)
        nurbs.Points[i] = new ControlPoint(points[i], payload.Weights[i]);
      for (int i = 0; i < payload.Knots.Count; i++)
        nurbs.Knots[i] = payload.Knots[i];

      if (!nurbs.IsValid)
        throw new InvalidOperationException("Failed to create NurbsCurve from payload.");
      return nurbs;
    }

    public static ProgesiNurbsPayload FromCurve(Curve curve, bool repairNonMonotonic = true)
    {
      if (curve == null) throw new ArgumentNullException(nameof(curve));
      var nurbs = curve.ToNurbsCurve();
      if (nurbs == null)
        throw new InvalidOperationException("Curve cannot be converted to NurbsCurve.");

      var cps = new List<(double X, double Value)>();
      var weights = new List<double>();
      for (int i = 0; i < nurbs.Points.Count; i++)
      {
        var pt = nurbs.Points[i].Location;
        cps.Add((pt.X, pt.Y));
        weights.Add(nurbs.Points[i].Weight);
      }

      var knots = new List<double>();
      for (int i = 0; i < nurbs.Knots.Count; i++)
        knots.Add(nurbs.Knots[i]);
      int degree = nurbs.Degree;

      if (!IsMonotonicInX(cps) && !repairNonMonotonic)
        throw new ArgumentException("Control point x coordinates are not monotonic non-decreasing.");

      if (!IsMonotonicInX(cps))
        cps = RepairMonotonic(cps, weights, out weights);

      return new ProgesiNurbsPayload(degree, cps, weights, knots);
    }

    private static bool IsMonotonicInX(IReadOnlyList<(double X, double Value)> cps)
    {
      for (int i = 1; i < cps.Count; i++)
      {
        if (cps[i].X + MonotonicTolerance < cps[i - 1].X)
          return false;
      }
      return true;
    }

    private static List<(double X, double Value)> RepairMonotonic(
      List<(double X, double Value)> cps,
      List<double> weights,
      out List<double> repairedWeights)
    {
      var pairs = cps
        .Select((cp, i) => (cp, w: weights[i]))
        .OrderBy(p => p.cp.X)
        .ToList();

      var merged = new List<(double X, double Value)>();
      var mergedWeights = new List<double>();
      foreach (var p in pairs)
      {
        if (merged.Count == 0 || p.cp.X > merged[merged.Count - 1].X + MonotonicTolerance)
        {
          merged.Add(p.cp);
          mergedWeights.Add(p.w);
        }
        else
        {
          merged[merged.Count - 1] = p.cp;
          mergedWeights[mergedWeights.Count - 1] = p.w;
        }
      }

      repairedWeights = mergedWeights;
      return merged;
    }
  }
}
