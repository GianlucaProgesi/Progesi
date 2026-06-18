using System;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  public enum AxisCurveMode
  {
    Curve3d = 0,
    PlanXY = 1,
    Profile = 2
  }

  public sealed class AxisContext
  {
    public Guid AxisGuid { get; }
    public string AxisName { get; }
    public Curve Curve3d { get; }
    public AxisCurveMode Mode { get; }

    public AxisContext(Guid axisGuid, string axisName, Curve curve3d, AxisCurveMode mode)
    {
      if (axisGuid == Guid.Empty) throw new ArgumentException("AxisGuid cannot be empty.", nameof(axisGuid));
      if (string.IsNullOrWhiteSpace(axisName)) throw new ArgumentException("AxisName is required.", nameof(axisName));
      AxisGuid = axisGuid;
      AxisName = axisName.Trim();
      Curve3d = curve3d ?? throw new ArgumentNullException(nameof(curve3d));
      Mode = mode;
    }

    public override string ToString() => $"{AxisName} ({Mode})";
  }
}
