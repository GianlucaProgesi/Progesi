using System;
using System.Collections.Generic;
using System.Globalization;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly
{
  public class RhinoVariableRepositorySmokeComponent : GH_Component
  {
    public RhinoVariableRepositorySmokeComponent()
        : base("Progesi Repo (Rhino) – Smoke", "ProgesiRepoTest",
               "Smoke test del repository Rhino: Save / GetAll / Delete / Reset",
               "Progesi", "Debug")
    { }

    public override Guid ComponentGuid => new Guid("7b3f5c5a-5e2d-47c3-9fb5-0b6f8b9b2d10");

    protected override System.Drawing.Bitmap Icon => new System.Drawing.Bitmap(24, 24); // opzionale

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui l'azione selezionata", GH_ParamAccess.item, false);
      p.AddTextParameter("Action", "Action", "save | getall | delete | reset", GH_ParamAccess.item, "getall");
      p.AddIntegerParameter("Id", "Id", "Id variabile (per save/delete)", GH_ParamAccess.item, 1);
      p.AddTextParameter("Name", "Name", "Nome variabile (per save)", GH_ParamAccess.item, "n");
      p.AddTextParameter("Value", "Value", "Valore (stringa). Se numerico, sarà interpretato come int/double", GH_ParamAccess.item, "42");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddBooleanParameter("Ok", "Ok", "Esito azione", GH_ParamAccess.item);
      p.AddTextParameter("Message", "Message", "Dettagli operazione / errori", GH_ParamAccess.item);
      p.AddIntegerParameter("Count", "Count", "Numero variabili salvate (getall/reset)", GH_ParamAccess.item);
      p.AddTextParameter("Vars", "Vars", "Elenco variabili (id:name=value)", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false; string action = "getall"; int id = 1; string name = "n"; string valueText = "42";
      da.GetData(0, ref run);
      da.GetData(1, ref action);
      da.GetData(2, ref id);
      da.GetData(3, ref name);
      da.GetData(4, ref valueText);

      bool ok = false; string msg = ""; int count = 0; var varsOut = new List<string>();
      if (!run)
      {
        da.SetData(0, ok);
        da.SetData(1, "Setta Run=true per eseguire");
        da.SetData(2, count);
        da.SetDataList(3, varsOut);
        return;
      }

      try
      {
        var repo = new RhinoVariableRepository(Rhino.RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc non disponibile"));

        switch ((action ?? "").Trim().ToLowerInvariant())
        {
          case "save":
            {
              object value = ParseValue(valueText);
              var v = new ProgesiVariable(id: id, name: name, value: value);
              var saved = repo.SaveAsync(v).GetAwaiter().GetResult();
              ok = true;
              msg = $"Salvata variabile {saved.Id}:{saved.Name} = {saved.Value}";
              break;
            }
          case "getall":
            {
              var all = repo.GetAllAsync().GetAwaiter().GetResult();
              count = all.Count;
              foreach (var v in all)
                varsOut.Add($"{v.Id}:{v.Name}={v.Value}");
              ok = true;
              msg = $"Letti {count} elementi";
              break;
            }
          case "delete":
            {
              var removed = repo.DeleteAsync(id).GetAwaiter().GetResult();
              ok = removed;
              msg = removed ? $"Cancellata variabile id={id}" : $"Nessuna variabile con id={id}";
              break;
            }
          case "reset":
            {
              var all = repo.GetAllAsync().GetAwaiter().GetResult();
              int removed = 0;
              if (all.Count > 0)
              {
                var ids = new List<int>();
                foreach (var v in all) ids.Add(v.Id);
                removed = repo.DeleteManyAsync(ids).GetAwaiter().GetResult();
              }
              count = repo.GetAllAsync().GetAwaiter().GetResult().Count;
              ok = true;
              msg = $"Reset: rimossi {removed}, ora {count} elementi";
              break;
            }
          default:
            msg = "Action non riconosciuta. Usa: save | getall | delete | reset";
            break;
        }
      }
      catch (Exception ex)
      {
        ok = false;
        msg = ex.GetType().Name + ": " + ex.Message;
      }

      da.SetData(0, ok);
      da.SetData(1, msg);
      da.SetData(2, count);
      da.SetDataList(3, varsOut);
    }

    private static object ParseValue(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return "";
      int i;
      double d;
      if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return i;
      if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
      if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
      if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
      return s; // fallback string
    }
  }
}

