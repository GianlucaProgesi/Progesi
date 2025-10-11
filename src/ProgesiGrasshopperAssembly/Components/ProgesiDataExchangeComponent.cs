using Grasshopper.Kernel;
using Progesi.DataExchange;
using ProgesiGrasshopperAssembly.Infrastructure;
using System;

namespace Progesi.GrasshopperAssembly.Components
{
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    public ProgesiDataExchangeComponent()
      : base("Progesi Data Exchange", "DataX",
             "Read/Write tra Rhino(local), SQLite e Excel (.xlsx).",
             "Progesi", "Data")
    { }

    public override Guid ComponentGuid => new Guid("E2D8D8E1-10D7-4F62-9A8E-A2D8E3A1B8C5");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;
    
    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui l'azione selezionata.", GH_ParamAccess.item, false);
      p.AddTextParameter("Action", "Action", "Read o Write (default: Read).", GH_ParamAccess.item, "Read");
      p.AddTextParameter("DbPath", "Db", "SQLite path (opzionale). In Write crea il file se assente.", GH_ParamAccess.item, "");
      p.AddTextParameter("ExcelPath", "Xlsx", "Excel .xlsx path (opzionale). In Write crea il file se assente.", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Info", "Info", "Log di esecuzione.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      RhinoBridgeBootstrap.EnsureConfigured();

      bool run = false;
      string action = "Read";
      string db = "";     // inizializza a stringa vuota
      string xlsx = "";

      da.GetData(0, ref run);
      da.GetData(1, ref action);
      da.GetData(2, ref db);
      da.GetData(3, ref xlsx);

      if (!run) { da.SetData(0, "Idle"); return; }

      var rhino = RhinoBridge.GetRhinoStore();
      if (rhino == null) { da.SetData(0, "Error: RhinoBridge non configurato."); return; }

      var act = string.Equals(action, "Write", StringComparison.OrdinalIgnoreCase)
                  ? DataExchangeAction.Write
                  : DataExchangeAction.Read;

      // Passiamo stringhe (vuote se assenti); la libreria gestisce white-space
      try
      {
        var report = DataExchangeRunner.Run(act, rhino, db, xlsx, createDbIfMissing: true);
        da.SetData(0, report.ToString());
      }
      catch (Exception ex)
      {
        da.SetData(0, "Error: " + ex.Message);
      }
    }
  }
}
