using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProgesiCore.Internal
{
  /// <summary>
  /// Pure-managed rational NURBS evaluator with x→parameter inversion for monotonic-in-x segments.
  /// Culture-invariant and deterministic; uses bisection for inversion (documented tolerance).
  /// </summary>
  internal static class ProgesiNurbsEvaluator
  {
    /// <summary>Default tolerance for x→parameter inversion (bisection stop criterion).</summary>
    public const double DefaultParameterTolerance = 1e-9;

    /// <summary>Maximum bisection iterations (deterministic cap).</summary>
    public const int MaxInversionIterations = 64;

    public static double? EvaluateAtX(
      ProgesiNurbsPayload nurbs,
      double queryX,
      double parameterTolerance = DefaultParameterTolerance)
    {
      if (nurbs == null) throw new ArgumentNullException(nameof(nurbs));

      var cps = nurbs.ControlPoints;
      if (cps.Count == 0)
        return null;

      double xMin = cps[0].X;
      double xMax = cps[cps.Count - 1].X;

      if (queryX < xMin - parameterTolerance || queryX > xMax + parameterTolerance)
        return null;

      if (Math.Abs(queryX - xMin) <= parameterTolerance)
        return EvaluateAtParameter(nurbs, nurbs.Knots[nurbs.Degree]).Value;

      if (Math.Abs(queryX - xMax) <= parameterTolerance)
        return EvaluateAtParameter(nurbs, nurbs.Knots[nurbs.Knots.Count - nurbs.Degree - 1]).Value;

      double uMin = nurbs.Knots[nurbs.Degree];
      double uMax = nurbs.Knots[nurbs.Knots.Count - nurbs.Degree - 1];

      double uLo = uMin;
      double uHi = uMax;

      for (int iter = 0; iter < MaxInversionIterations; iter++)
      {
        double uMid = (uLo + uHi) * 0.5;
        double xMid = EvaluateAtParameter(nurbs, uMid).X;

        if (Math.Abs(xMid - queryX) <= parameterTolerance)
          return EvaluateAtParameter(nurbs, uMid).Value;

        if (xMid < queryX)
          uLo = uMid;
        else
          uHi = uMid;
      }

      return EvaluateAtParameter(nurbs, (uLo + uHi) * 0.5).Value;
    }

    internal static (double X, double Value) EvaluateAtParameter(ProgesiNurbsPayload nurbs, double u)
    {
      int degree = nurbs.Degree;
      var knots = nurbs.Knots;
      var cps = nurbs.ControlPoints;
      var weights = nurbs.Weights;
      int n = cps.Count - 1;

      u = ClampParameter(u, degree, knots);
      int span = FindSpan(n, degree, u, knots);

      double[] basis = BasisFunctions(span, degree, u, knots);

      double wx = 0.0;
      double wy = 0.0;
      double wSum = 0.0;

      for (int j = 0; j <= degree; j++)
      {
        int idx = span - degree + j;
        double w = basis[j] * weights[idx];
        wSum += w;
        wx += w * cps[idx].X;
        wy += w * cps[idx].Value;
      }

      if (Math.Abs(wSum) < double.Epsilon)
        throw new InvalidOperationException("Rational NURBS weight sum is zero at u=" + u.ToString(CultureInfo.InvariantCulture));

      return (wx / wSum, wy / wSum);
    }

    private static double ClampParameter(double u, int degree, IReadOnlyList<double> knots)
    {
      double uMin = knots[degree];
      double uMax = knots[knots.Count - degree - 1];
      if (u < uMin) return uMin;
      if (u > uMax) return uMax;
      return u;
    }

    private static int FindSpan(int n, int degree, double u, IReadOnlyList<double> knots)
    {
      if (u >= knots[n + 1])
        return n;

      int low = degree;
      int high = n + 1;
      int mid = (low + high) / 2;

      while (u < knots[mid] || u >= knots[mid + 1])
      {
        if (u < knots[mid])
          high = mid;
        else
          low = mid;
        mid = (low + high) / 2;
      }

      return mid;
    }

    private static double[] BasisFunctions(int span, int degree, double u, IReadOnlyList<double> knots)
    {
      var left = new double[degree + 1];
      var right = new double[degree + 1];
      var ndu = new double[degree + 1, degree + 1];
      ndu[0, 0] = 1.0;

      for (int j = 1; j <= degree; j++)
      {
        left[j] = u - knots[span + 1 - j];
        right[j] = knots[span + j] - u;
        double saved = 0.0;

        for (int r = 0; r < j; r++)
        {
          ndu[j, r] = right[r + 1] + left[j - r];
          double temp = ndu[r, j - 1] / ndu[j, r];
          ndu[r, j] = saved + right[r + 1] * temp;
          saved = left[j - r] * temp;
        }

        ndu[j, j] = saved;
      }

      var basis = new double[degree + 1];
      for (int j = 0; j <= degree; j++)
        basis[j] = ndu[j, degree];

      return basis;
    }
  }
}
