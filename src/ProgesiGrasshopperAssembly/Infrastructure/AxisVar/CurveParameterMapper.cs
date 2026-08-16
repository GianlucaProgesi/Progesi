using System;
using ProgesiCore;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>
  /// Exact normalized [0,1] ↔ real arc-length ↔ curve parameter ↔ 3D point conversions
  /// using Rhino true arc-length (never linear total-length scaling).
  /// </summary>
  public sealed class CurveParameterMapper
  {
    /// <summary>Documented tolerance for length/parameter queries (matches Core axis tolerance).</summary>
    public const double LengthTolerance = ProgesiAxisVariable.DefaultTolerance;

    private readonly Curve _sourceCurve;
    private readonly Curve _abscissaCurve;
    private readonly ProgesiCore.AxisCurveMode _mode;
    private readonly double _totalLength;

    public CurveParameterMapper(Curve curve3d, ProgesiCore.AxisCurveMode mode)
    {
      if (curve3d == null) throw new ArgumentNullException(nameof(curve3d));
      if (!curve3d.IsValid) throw new ArgumentException("Curve is invalid.", nameof(curve3d));

      _sourceCurve = curve3d.DuplicateCurve();
      _mode = mode;

      switch (mode)
      {
        case ProgesiCore.AxisCurveMode.Curve3d:
          _abscissaCurve = _sourceCurve.DuplicateCurve();
          break;
        case ProgesiCore.AxisCurveMode.PlanXY:
        case ProgesiCore.AxisCurveMode.Profile:
          _abscissaCurve = ProjectionService.ProjectToPlanXY(_sourceCurve);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown axis curve mode.");
      }

      _totalLength = _abscissaCurve.GetLength();
      if (_totalLength <= LengthTolerance)
        throw new InvalidOperationException("Abscissa curve length is zero.");
    }

    public double TotalLength => _totalLength;
    public ProgesiCore.AxisCurveMode Mode => _mode;
    public Curve SourceCurve => _sourceCurve;
    public Curve AbscissaCurve => _abscissaCurve;

    public double NormalizedToReal(double normalized)
    {
      ValidateNormalized(normalized);
      return normalized * _totalLength;
    }

    public double RealToNormalized(double realLength)
    {
      ValidateReal(realLength);
      return realLength / _totalLength;
    }

    public bool TryNormalizedToParameter(double normalized, out double t)
    {
      t = 0.0;
      if (!ValidateNormalizedSoft(normalized)) return false;
      return TryRealToParameter(NormalizedToReal(normalized), out t);
    }

    public bool TryRealToParameter(double realLength, out double t)
    {
      t = 0.0;
      if (!ValidateRealSoft(realLength)) return false;

      realLength = ClampReal(realLength);
      double tAbs;
      if (!_abscissaCurve.LengthParameter(realLength, out tAbs, LengthTolerance))
        return false;

      if (_mode == ProgesiCore.AxisCurveMode.Curve3d)
      {
        t = tAbs;
        return true;
      }

      var ptAbs = _abscissaCurve.PointAt(tAbs);
      return _sourceCurve.ClosestPoint(ptAbs, out t);
    }

    public bool TryParameterToReal(double t, out double realLength)
    {
      realLength = 0.0;
      if (!_sourceCurve.IsValid) return false;

      if (_mode == ProgesiCore.AxisCurveMode.Curve3d)
      {
        realLength = _sourceCurve.GetLength(new Interval(_sourceCurve.Domain.T0, t));
        return true;
      }

      var pt = _sourceCurve.PointAt(t);
      var planPt = new Point3d(pt.X, pt.Y, 0.0);
      double tPlan;
      if (!_abscissaCurve.ClosestPoint(planPt, out tPlan))
        return false;
      realLength = _abscissaCurve.GetLength(new Interval(_abscissaCurve.Domain.T0, tPlan));
      return true;
    }

    public bool TryParameterToNormalized(double t, out double normalized)
    {
      normalized = 0.0;
      if (!TryParameterToReal(t, out double real)) return false;
      normalized = real / _totalLength;
      return true;
    }

    public Point3d ParameterToPoint3d(double t) => _sourceCurve.PointAt(t);

    public double GetElevationAtParameter(double t) => _sourceCurve.PointAt(t).Z;

    public bool TryRealToPoint3d(double realLength, out Point3d point)
    {
      point = Point3d.Unset;
      if (!TryRealToParameter(realLength, out double t)) return false;
      point = ParameterToPoint3d(t);
      return point.IsValid;
    }

    public bool TryNormalizedToPoint3d(double normalized, out Point3d point)
      => TryRealToPoint3d(NormalizedToReal(normalized), out point);

    private static void ValidateNormalized(double normalized)
    {
      if (double.IsNaN(normalized) || double.IsInfinity(normalized))
        throw new ArgumentOutOfRangeException(nameof(normalized), "Normalized station must be finite.");
      if (normalized < -LengthTolerance || normalized > 1.0 + LengthTolerance)
        throw new ArgumentOutOfRangeException(nameof(normalized), "Normalized station must be within [0,1].");
    }

    private static bool ValidateNormalizedSoft(double normalized)
    {
      if (double.IsNaN(normalized) || double.IsInfinity(normalized)) return false;
      return normalized >= -LengthTolerance && normalized <= 1.0 + LengthTolerance;
    }

    private static void ValidateReal(double realLength)
    {
      if (double.IsNaN(realLength) || double.IsInfinity(realLength))
        throw new ArgumentOutOfRangeException(nameof(realLength), "Real station must be finite.");
    }

    private static bool ValidateRealSoft(double realLength)
    {
      if (double.IsNaN(realLength) || double.IsInfinity(realLength)) return false;
      return true;
    }

    private double ClampReal(double realLength)
      => Math.Max(0.0, Math.Min(_totalLength, realLength));
  }
}
