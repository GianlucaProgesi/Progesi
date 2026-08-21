#nullable enable
using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableDiagramSettingsComponent : GH_Component
  {
    public AxisVariableDiagramSettingsComponent()
      : base("AxisVariable.DiagramSettings", "AxDiagSet",
        "Build DiagramSettings for AxisVariable.Diagram.",
        "Progesi", "Diagrams & Tables")
    { }

    public override Guid ComponentGuid => new Guid("c4e8f1a2-3b6d-4e9f-a871-2d5c6b8e0f13");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddNumberParameter("TargetWidth", "W", "Target diagram width (mm).", GH_ParamAccess.item, 200.0);
      p.AddNumberParameter("TargetHeight", "H", "Target diagram height (mm).", GH_ParamAccess.item, 120.0);
      p.AddBooleanParameter("Grid", "G", "Draw background grid.", GH_ParamAccess.item, false);
      p.AddIntegerParameter("Decimals", "D", "Decimal places for tick labels.", GH_ParamAccess.item, 2);
      p.AddTextParameter("Title", "T", "Ordinate axis title.", GH_ParamAccess.item, "Value");
      p.AddTextParameter("AbscissaUnit", "Su", "Abscissa unit suffix.", GH_ParamAccess.item, "m");
      p.AddTextParameter("OrdinateUnit", "Ou", "Ordinate unit suffix.", GH_ParamAccess.item, string.Empty);
      p.AddBooleanParameter("Legend", "L", "Show series legend.", GH_ParamAccess.item, true);

      for (int i = 0; i < p.ParamCount; i++)
        p[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Settings", "S", "DiagramSettings object.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      var settings = AxisVarPresentationGhSupport.DefaultDiagramSettings();

      double width = settings.TargetBoxWidthMm;
      if (da.GetData(0, ref width) && width > 0)
        settings.TargetBoxWidthMm = width;

      double height = settings.TargetBoxHeightMm;
      if (da.GetData(1, ref height) && height > 0)
        settings.TargetBoxHeightMm = height;

      bool grid = settings.GridEnabled;
      da.GetData(2, ref grid);
      settings.GridEnabled = grid;

      int decimals = settings.ValueDecimals;
      if (da.GetData(3, ref decimals) && decimals >= 0)
      {
        settings.ValueDecimals = decimals;
        settings.StationDecimals = decimals;
      }

      string title = settings.OrdinateTitle;
      da.GetData(4, ref title);
      if (!string.IsNullOrWhiteSpace(title))
        settings.OrdinateTitle = title;

      string abscissaUnit = settings.AbscissaUnit;
      da.GetData(5, ref abscissaUnit);
      if (abscissaUnit != null)
        settings.AbscissaUnit = abscissaUnit;

      string ordinateUnit = settings.OrdinateUnit;
      da.GetData(6, ref ordinateUnit);
      if (ordinateUnit != null)
        settings.OrdinateUnit = ordinateUnit;

      bool legend = settings.ShowLegend;
      da.GetData(7, ref legend);
      settings.ShowLegend = legend;

      da.SetData(0, new GH_ObjectWrapper(settings));
    }
  }
}
