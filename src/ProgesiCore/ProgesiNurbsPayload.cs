using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore
{
  /// <summary>
  /// Rhino-free rational NURBS payload for a value-curve segment (2D: x → value).
  /// </summary>
  public sealed class ProgesiNurbsPayload : ValueObject
  {
    public int Degree { get; }
    public IReadOnlyList<(double X, double Value)> ControlPoints { get; }
    public IReadOnlyList<double> Weights { get; }
    public IReadOnlyList<double> Knots { get; }

    public ProgesiNurbsPayload(
      int degree,
      IEnumerable<(double X, double Value)> controlPoints,
      IEnumerable<double> weights,
      IEnumerable<double> knots)
    {
      Guard.Against.Negative(degree, nameof(degree));
      Guard.Against.Null(controlPoints, nameof(controlPoints));
      Guard.Against.Null(weights, nameof(weights));
      Guard.Against.Null(knots, nameof(knots));

      var cpList = controlPoints.ToList();
      var weightList = weights.ToList();
      var knotList = knots.ToList();

      Validate(degree, cpList, weightList, knotList);

      Degree = degree;
      ControlPoints = cpList.AsReadOnly();
      Weights = weightList.AsReadOnly();
      Knots = knotList.AsReadOnly();
    }

    internal static void Validate(
      int degree,
      IReadOnlyList<(double X, double Value)> controlPoints,
      IReadOnlyList<double> weights,
      IReadOnlyList<double> knots)
    {
      if (degree < 1)
        throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be >= 1.");

      int cpCount = controlPoints.Count;
      if (cpCount < degree + 1)
        throw new ArgumentException($"At least {degree + 1} control points are required.", nameof(controlPoints));

      if (weights.Count != cpCount)
        throw new ArgumentException("Weights count must match control point count.", nameof(weights));

      int expectedKnots = cpCount + degree + 1;
      if (knots.Count != expectedKnots)
        throw new ArgumentException($"Knot vector length must be {expectedKnots} (cp + degree + 1).", nameof(knots));

      for (int i = 0; i < cpCount; i++)
      {
        var (x, value) = controlPoints[i];
        if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(value) || double.IsInfinity(value))
          throw new ArgumentOutOfRangeException(nameof(controlPoints), "Control points must be finite.");
        if (weights[i] <= 0.0 || double.IsNaN(weights[i]) || double.IsInfinity(weights[i]))
          throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be positive and finite.");
      }

      for (int i = 1; i < cpCount; i++)
      {
        if (controlPoints[i].X + ProgesiAxisVariable.DefaultTolerance < controlPoints[i - 1].X)
          throw new ArgumentException("Control point x coordinates must be monotonic non-decreasing.", nameof(controlPoints));
      }

      for (int i = 1; i < knots.Count; i++)
      {
        if (knots[i] + ProgesiAxisVariable.DefaultTolerance < knots[i - 1])
          throw new ArgumentException("Knot vector must be non-decreasing.", nameof(knots));
      }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Degree;
      foreach (var cp in ControlPoints)
      {
        yield return cp.X;
        yield return cp.Value;
      }
      foreach (double w in Weights)
        yield return w;
      foreach (double k in Knots)
        yield return k;
    }
  }
}
