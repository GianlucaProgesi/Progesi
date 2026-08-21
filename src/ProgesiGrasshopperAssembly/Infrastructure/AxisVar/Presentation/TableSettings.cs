namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>Presentation options for the station table block (B3c).</summary>
  public sealed class TableSettings
  {
    public int ValueDecimals { get; set; } = 2;
    public int StationDecimals { get; set; } = 3;
    public bool ShowUnitsInHeader { get; set; } = true;
    public string StationColumnHeader { get; set; } = "S (m)";
    public string IdColumnHeader { get; set; } = "ID";
    public double CellWidthMm { get; set; } = 18.0;
    public double CellHeightMm { get; set; } = 4.0;
    public double TextHeightMm { get; set; } = 2.0;
    public double GapFromDiagramMm { get; set; } = 10.0;
  }
}
