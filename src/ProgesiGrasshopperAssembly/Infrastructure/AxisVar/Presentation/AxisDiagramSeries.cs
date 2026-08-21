using ProgesiCore;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  public sealed class AxisDiagramSeries
  {
    public AxisDiagramSeries(
      ProgesiAxisVariable axis,
      CurveParameterMapper mapper,
      int colorArgb,
      string legendLabel)
    {
      Axis = axis ?? throw new System.ArgumentNullException(nameof(axis));
      Mapper = mapper ?? throw new System.ArgumentNullException(nameof(mapper));
      ColorArgb = colorArgb;
      LegendLabel = legendLabel ?? axis.Name;
    }

    public ProgesiAxisVariable Axis { get; }
    public CurveParameterMapper Mapper { get; }
    public int ColorArgb { get; }
    public string LegendLabel { get; }
  }
}
