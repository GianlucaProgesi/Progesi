#nullable disable
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Progesi.Grasshopper.Browsers.Internal;

namespace Progesi.Grasshopper.Browsers.Components
{
  public sealed class ProgesiVariableBrowserComponent : GH_Component
  {
    public ProgesiVariableBrowserComponent()
      : base("Progesi Variable Browser", "VarBrowser",
             "Vista read-only della tabella 'variables' con filtri opzionali e export CSV.",
             "Progesi", "Browsers")
    { }

    public override Guid ComponentGuid => new Guid("B7F79F5C-7E8A-4B83-9A2E-6D30E6A0B1A2");
    protected override System.Drawing.Bitmap Icon => null;
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run",
        "Esegui la query. False = nessuna operazione.", GH_ParamAccess.item, false);

      p.AddTextParameter("DbPath", "Db",
        "Percorso SQLite (.db). Default: data\\progesi.db", GH_ParamAccess.item, @"data\progesi.db");

      p.AddTextParameter("FilterHash", "Hash",
        "Filtro 'contiene' su Hash (opzionale).", GH_ParamAccess.item, "");

      p.AddTextParameter("FilterName", "Name",
        "Filtro 'contiene' su Name (se la colonna esiste).", GH_ParamAccess.item, "");

      p.AddTextParameter("FilterBy", "By",
        "Filtro su By/CreatedBy/Author/ModifiedBy (prima colonna disponibile).", GH_ParamAccess.item, "");

      p.AddIntegerParameter("Limit", "N",
        "Numero massimo righe (1..1000).", GH_ParamAccess.item, 200);

      p.AddTextParameter("CsvPath", "Csv",
        "Se impostato e Export=true, salva le righe visibili in CSV.", GH_ParamAccess.item, @"out\variables_browser.csv");

      p.AddBooleanParameter("Export", "Exp",
        "Se true, esporta le righe visibili in CsvPath.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Headers", "H", "Intestazioni della tabella.", GH_ParamAccess.list);
      p.AddTextParameter("Rows", "R", "Righe flatten (usa 'Tree' per struttura cella a cella).", GH_ParamAccess.list);
      p.AddTextParameter("Info", "Info", "Esito o errori.", GH_ParamAccess.item);
      p.AddIntegerParameter("Count", "#", "Numero di righe restituite.", GH_ParamAccess.item);
      p.AddTextParameter("Tree", "Tree", "Righe come albero: ogni branch è una riga.", GH_ParamAccess.tree);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false, exp = false; string db = null, fh = null, fn = null, fby = null, csv = null; int limit = 200;
      da.GetData(0, ref run);
      da.GetData(1, ref db);
      da.GetData(2, ref fh);
      da.GetData(3, ref fn);
      da.GetData(4, ref fby);
      da.GetData(5, ref limit);
      da.GetData(6, ref csv);
      da.GetData(7, ref exp);

      if (!run)
      {
        da.SetDataList(0, new List<string>()); da.SetDataList(1, new List<string>());
        da.SetData(2, "Idle"); da.SetData(3, 0); da.SetDataTree(4, new GH_Structure<GH_String>());
        return;
      }

      try
      {
        using (var b = new SqliteBrowser(db))
        {
          var (headers, rows) = b.BrowseVariables(fh ?? "", fn ?? "", fby ?? "", limit);
          da.SetDataList(0, headers);

          var flat = new List<string>(rows.Count);
          var tree = new GH_Structure<GH_String>();
          for (int i = 0; i < rows.Count; i++)
          {
            var row = rows[i];
            flat.Add(string.Join(" | ", row));
            var path = new GH_Path(i);
            foreach (var cell in row) tree.Append(new GH_String(cell), path);
          }
          da.SetDataList(1, flat);
          da.SetData(2, $"OK ({rows.Count} rows)");
          da.SetData(3, rows.Count);
          da.SetDataTree(4, tree);

          if (exp && !string.IsNullOrWhiteSpace(csv))
          {
            CsvUtils.Write(csv, headers, rows);
            da.SetData(2, $"OK ({rows.Count} rows) | CSV: {csv}");
          }
        }
      }
      catch (Exception ex)
      {
        da.SetDataList(0, new List<string>()); da.SetDataList(1, new List<string>());
        da.SetData(2, "Error: " + ex.Message); da.SetData(3, 0); da.SetDataTree(4, new GH_Structure<GH_String>());
      }
    }
  }
}
