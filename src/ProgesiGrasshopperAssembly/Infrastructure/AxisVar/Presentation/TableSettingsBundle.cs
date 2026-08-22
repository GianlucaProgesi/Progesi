namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  /// <summary>TableSettings plus optional default unit suffix for GH wiring.</summary>
  public sealed class TableSettingsBundle
  {
    public TableSettingsBundle(TableSettings settings, string defaultUnit)
    {
      Settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
      DefaultUnit = defaultUnit ?? string.Empty;
    }

    public TableSettings Settings { get; }
    public string DefaultUnit { get; }
  }
}
