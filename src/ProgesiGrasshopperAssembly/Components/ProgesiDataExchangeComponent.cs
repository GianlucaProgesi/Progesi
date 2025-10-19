#nullable disable
using Grasshopper.Kernel;
using Progesi.DataExchange;
using ProgesiGrasshopperAssembly.Infrastructure;
using System;
using System.Drawing;

// PATCH: reflection per Dirty-mark
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace Progesi.GrasshopperAssembly.Components
{
  public sealed class ProgesiDataExchangeComponent : GH_Component
  {
    public ProgesiDataExchangeComponent()
      : base("Progesi Data Exchange", "DataX",
             "Import/Export tra Rhino (locale), SQLite (.db) ed Excel (.xlsx).",
             "Progesi", "Data")
    { }

    public override Guid ComponentGuid => new Guid("E2D8D8E1-10D7-4F62-9A8E-A2D8E3A1B8C5");
    protected override Bitmap Icon => ProgesiIcons.DataEx;
    public override GH_Exposure Exposure => GH_Exposure.primary;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run",
        "Esegui l'azione selezionata. False = nessuna operazione.", GH_ParamAccess.item, false);

      p.AddTextParameter("Action", "Action",
        "Read = importa verso Rhino. Write = esporta da Rhino. Default: Read.", GH_ParamAccess.item, "Read");

      p.AddTextParameter("DbPath", "Db",
        "Percorso file SQLite (.db). In Write il file viene creato se non esiste.", GH_ParamAccess.item, "");

      p.AddTextParameter("ExcelPath", "Xlsx",
        "Percorso file Excel (.xlsx). In Write il file viene creato se non esiste.", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
      => p.AddTextParameter("Info", "Info", "Log sintetico dell'operazione (ins/upd/skip + destinazioni).", GH_ParamAccess.item);

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false; string action = "Read"; string db = ""; string xlsx = "";
      da.GetData(0, ref run);
      da.GetData(1, ref action);
      da.GetData(2, ref db);
      da.GetData(3, ref xlsx);

      if (!run) { da.SetData(0, "Idle"); return; }

      // bootstrap
      RhinoBridgeBootstrap.EnsureConfigured();
      var store = RhinoBridge.GetRhinoStore();
      if (store == null) { da.SetData(0, "Error: RhinoBridge non configurato."); return; }

      var act = string.Equals(action, "Write", StringComparison.OrdinalIgnoreCase)
                ? DataExchangeAction.Write
                : DataExchangeAction.Read;

      try
      {
        // PATCH: prima di scrivere → marca tutto “dirty” (robustezza GH 8.21/8.22)
        if (act == DataExchangeAction.Write)
          MarkAllAsDirtySafe(store);

        var report = DataExchangeRunner.Run(act, store, db, xlsx, createDbIfMissing: true);

        // PATCH: dopo un Read → marca dirty per le solve successive (Write)
        if (act == DataExchangeAction.Read)
          MarkAllAsDirtySafe(store);

        da.SetData(0, report?.ToString() ?? "OK");
      }
      catch (Exception ex)
      {
        da.SetData(0, "Error: " + ex.Message);
      }
    }

    // ---------------- helpers PATCH ----------------

    /// <summary>Marca come “dirty” tutte le entità presenti nello Store.
    /// Tenta prima Store.MarkAllAsDirty(); se assente, setta IsDirty=true via reflection.</summary>
    private static void MarkAllAsDirtySafe(object store)
    {
      if (store == null) return;

      // 1) metodo diretto, se esiste
      var m = store.GetType().GetMethod("MarkAllAsDirty",
              BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (m != null) { try { m.Invoke(store, null); return; } catch { } }

      // 2) altrimenti: attraversa le collezioni e imposta IsDirty=true
      foreach (var it in Enumerate(store, "Variables")) SetBool(it, "IsDirty", true);
      foreach (var it in Enumerate(store, "Metadata")) SetBool(it, "IsDirty", true);
      foreach (var it in Enumerate(store, "AxisVars", "AxisVariables", "Axis")) SetBool(it, "IsDirty", true);
    }

    private static IEnumerable<object> Enumerate(object store, params string[] propNames)
    {
      if (store == null) yield break;
      foreach (var pn in propNames ?? Array.Empty<string>())
      {
        var p = store.GetType().GetProperty(pn,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p == null) continue;

        if (p.GetValue(store) is IEnumerable en)
        {
          foreach (var it in en) yield return it;  // non-generic → object
          yield break;
        }
      }
    }

    private static void SetBool(object o, string prop, bool value)
    {
      if (o == null) return;
      try
      {
        var p = o.GetType().GetProperty(prop,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite)
          p.SetValue(o, value, null);
      }
      catch { /* ignore */ }
    }
  }
}
