using ProgesiCore;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  /// <summary>Lightweight handle passed between AxisVar GH components.</summary>
  public sealed class AxisVarHandle
  {
    public int AxisId { get; }
    public ProgesiAxisVariable Axis { get; }

    public AxisVarHandle(int axisId, ProgesiAxisVariable axis)
    {
      AxisId = axisId;
      Axis = axis ?? throw new System.ArgumentNullException(nameof(axis));
    }

    public override string ToString() => $"Axis#{AxisId} {Axis.AxisName}/{Axis.Name}";
  }
}
