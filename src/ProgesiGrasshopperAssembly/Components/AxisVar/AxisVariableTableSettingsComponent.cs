#nullable enable
using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableTableSettingsComponent : GH_Component
  {
    public AxisVariableTableSettingsComponent()
      : base("AxisVariable.TableSettings", "AxTblSet",
        "Build TableSettings for AxisVariable.Table.",
        "Progesi", "Diagrams & Tables")
    { }

    public override Guid ComponentGuid => new Guid("b3d7e5a1-9f42-4c86-b2d0-1e8a6f3c5d79");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddIntegerParameter("Decimals", "D", "Decimal places for numeric values.", GH_ParamAccess.item, 2);
      p.AddIntegerParameter("StationDecimals", "Sd", "Decimal places for station column.", GH_ParamAccess.item, 3);
      p.AddTextParameter("Unit", "U", "Default unit suffix for value column headers.", GH_ParamAccess.item, string.Empty);
      p.AddBooleanParameter("ShowUnits", "Su", "Include units in column headers.", GH_ParamAccess.item, true);
      p.AddTextParameter("StationHeader", "Sh", "Station column header.", GH_ParamAccess.item, "S (m)");
      p.AddTextParameter("IdHeader", "Ih", "Station Id column header.", GH_ParamAccess.item, "ID");

      for (int i = 0; i < p.ParamCount; i++)
        p[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Settings", "S", "TableSettings object.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      var settings = AxisVarPresentationGhSupport.DefaultTableSettings();

      int decimals = settings.ValueDecimals;
      if (da.GetData(0, ref decimals) && decimals >= 0)
        settings.ValueDecimals = decimals;

      int stationDecimals = settings.StationDecimals;
      if (da.GetData(1, ref stationDecimals) && stationDecimals >= 0)
        settings.StationDecimals = stationDecimals;

      string unit = string.Empty;
      da.GetData(2, ref unit);

      bool showUnits = settings.ShowUnitsInHeader;
      da.GetData(3, ref showUnits);
      settings.ShowUnitsInHeader = showUnits;

      string stationHeader = settings.StationColumnHeader;
      da.GetData(4, ref stationHeader);
      if (!string.IsNullOrWhiteSpace(stationHeader))
        settings.StationColumnHeader = stationHeader;

      string idHeader = settings.IdColumnHeader;
      da.GetData(5, ref idHeader);
      if (!string.IsNullOrWhiteSpace(idHeader))
        settings.IdColumnHeader = idHeader;

      var wrapper = new TableSettingsBundle(settings, unit ?? string.Empty);
      da.SetData(0, new GH_ObjectWrapper(wrapper));
    }
  }
}
