namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>Presentation options for the 2D value-vs-station diagram (B3c).</summary>
  public sealed class DiagramSettings
  {
    public bool AutoFit { get; set; } = true;
    public bool GridEnabled { get; set; } = false;
    public double TargetBoxWidthMm { get; set; } = 200.0;
    public double TargetBoxHeightMm { get; set; } = 120.0;
    public double OriginOffsetMm { get; set; } = 10.0;
    public int TargetTickCount { get; set; } = 5;
    public int ValueDecimals { get; set; } = 2;
    public int StationDecimals { get; set; } = 3;
    public bool ShowLegend { get; set; } = true;
    public bool ShowTitles { get; set; } = true;
    public string AbscissaTitle { get; set; } = "Station";
    public string OrdinateTitle { get; set; } = "Value";
    public string AbscissaUnit { get; set; } = "m";
    public string OrdinateUnit { get; set; } = string.Empty;
    public int SampleCountPerSegment { get; set; } = 8;
    public double GridGreyFactor { get; set; } = 0.75;
    public int[] SeriesColorsArgb { get; set; } = new[]
    {
      unchecked((int)0xFF0072B2),
      unchecked((int)0xFFD55E00),
      unchecked((int)0xFF009E73),
      unchecked((int)0xFFCC79A7),
      unchecked((int)0xFF56B4E9),
      unchecked((int)0xFFE69F00)
    };
  }
}
