using System;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>
  /// Computes independent X/Y scale factors and nice-number tick spacing for diagram autofit.
  /// Pure managed — no Rhino types.
  /// </summary>
  public static class ScaleFactorOptimiser
  {
    private const double Epsilon = 1e-9;

    public static ScaleModel Optimise(
      double abscissaMax,
      double ordinateMin,
      double ordinateMax,
      DiagramSettings settings)
    {
      if (settings == null) throw new ArgumentNullException(nameof(settings));
      if (abscissaMax <= 0)
        throw new ArgumentOutOfRangeException(nameof(abscissaMax), "Abscissa max must be positive.");

      double ordMin = ordinateMin;
      double ordMax = ordinateMax;
      if (ordMax < ordMin)
      {
        var tmp = ordMin;
        ordMin = ordMax;
        ordMax = tmp;
      }

      double valueRange = ordMax - ordMin;
      if (valueRange <= Epsilon)
      {
        var mid = (ordMin + ordMax) * 0.5;
        ordMin = mid - 0.5;
        ordMax = mid + 0.5;
        valueRange = 1.0;
      }

      double offset = Math.Max(0.0, settings.OriginOffsetMm);
      double availW = Math.Max(1.0, settings.TargetBoxWidthMm - 2.0 * offset);
      double availH = Math.Max(1.0, settings.TargetBoxHeightMm - 2.0 * offset);

      double scaleX = availW / abscissaMax;
      double scaleY = availH / valueRange;

      int tickTarget = Math.Max(2, settings.TargetTickCount);
      double majorX = NiceStep(abscissaMax, tickTarget);
      double majorY = NiceStep(valueRange, tickTarget);
      double minorX = majorX * 0.5;
      double minorY = majorY * 0.5;

      double plotW = abscissaMax * scaleX;
      double plotH = valueRange * scaleY;

      return new ScaleModel(
        scaleX,
        scaleY,
        majorX,
        minorX,
        majorY,
        minorY,
        offset,
        offset,
        plotW,
        plotH,
        0.0,
        abscissaMax,
        ordMin,
        ordMax);
    }

    /// <summary>Rounds a raw step to 1/2/5 × 10ⁿ.</summary>
    public static double NiceStep(double range, int targetTickCount)
    {
      if (range <= Epsilon)
        return 1.0;

      double rough = range / Math.Max(1, targetTickCount);
      if (rough <= Epsilon)
        return 1.0;

      double exp = Math.Floor(Math.Log10(rough));
      double mag = Math.Pow(10.0, exp);
      double norm = rough / mag;

      double niceNorm;
      if (norm <= 1.0)
        niceNorm = 1.0;
      else if (norm <= 2.0)
        niceNorm = 2.0;
      else if (norm <= 5.0)
        niceNorm = 5.0;
      else
        niceNorm = 10.0;

      return niceNorm * mag;
    }
  }
}
