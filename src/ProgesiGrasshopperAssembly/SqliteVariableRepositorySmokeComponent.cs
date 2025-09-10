#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiRepositories.Sqlite;

namespace ProgesiGrasshopperAssembly
{
  public class SqliteVariableRepositorySmokeComponent : GH_Component
  {
    public SqliteVariableRepositorySmokeComponent()
        : base("Progesi Repo (SQLite) – Smoke", "ProgesiSqliteTest",
               "Smoke test del repository SQLite: Save / GetAll / Delete / Reset",
               "Progesi", "Debug")
    { }

    public override Guid ComponentGuid => new Guid("2A5D3F35-3C0E-4C3D-91E0-9E1F2B60C9A5");

    // Evita CS8603 (mai restituire null)
    protected override Bitmap Icon => new Bitmap(24, 24);

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddTextParameter("dbPath", "DB", "Percorso file .sqlite/.db (opzionale)", GH_ParamAccess.item, "");
      p.AddTextParameter("command", "Cmd", "save|getall|delete|reset", GH_ParamAccess.item, "getall");
      p.AddIntegerParameter("id", "Id", "Id variabile", GH_ParamAccess.item, 0);
      p.AddTextParameter("name", "Name", "Nome variabile (per save)", GH_ParamAccess.item, "");
      p.AddTextParameter("value", "Value", "Valore (string/int/double/bool)", GH_ParamAccess.item, "");
      p.AddTextParameter("dependsFrom", "Deps", "CSV di Id dipendenze (es: 1,2,3)", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddBooleanParameter("ok", "OK", "Esito", GH_ParamAccess.item);
      p.AddIntegerParameter("count", "N", "Conteggio", GH_ParamAccess.item);
      p.AddTextParameter("message", "Msg", "Messaggio", GH_ParamAccess.item);
      p.AddTextParameter("variables", "Vars", "Lista variabili", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      // NOTA: usiamo string NON-nullable con default sicuri
      string dbPath = string.Empty;
      string cmd = "getall";
      string name = string.Empty;
      string valStr = string.Empty;
      string depsCsv = string.Empty;
      int id = 0;

      // Leggi input; se non arrivano, restano i default NON-null
      da.GetData(0, ref dbPath);
      da.GetData(1, ref cmd);
      da.GetData(2, ref id);
      da.GetData(3, ref name);
      da.GetData(4, ref valStr);
      da.GetData(5, ref depsCsv);

      // Fallback sicuro per il path del DB (chiude CS8604)
      string fallbackPath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "Progesi", "progesi.sqlite");

      string safePath = string.IsNullOrWhiteSpace(dbPath) ? fallbackPath : dbPath;
      string? dir = Path.GetDirectoryName(safePath);
      Directory.CreateDirectory(string.IsNullOrEmpty(dir) ? "." : dir);

      var repo = new SqliteVariableRepository(safePath);

      bool ok = false;
      int count = 0;
      string msg = "";
      var varsOut = new List<string>();

      switch ((cmd ?? "").Trim().ToLowerInvariant())
      {
        case "save":
          {
            var deps = ParseDeps(depsCsv);
            var val = ParseValue(valStr);
            var v = new ProgesiVariable(id, name, val, deps);
            repo.SaveAsync(v).GetAwaiter().GetResult();
            ok = true; msg = "Salvato";
            break;
          }

        case "getall":
          {
            var all = repo.GetAllAsync().GetAwaiter().GetResult();
            count = all.Count;
            foreach (var v in all)
            {
              var n = v.Name ?? string.Empty;
              var val = v.Value is null ? "" : v.Value.ToString();
              varsOut.Add($"{v.Id}:{n}={val}");
            }
            ok = true; msg = $"Letti {count} elementi";
            break;
          }

        case "delete":
          {
            var removed = repo.DeleteAsync(id).GetAwaiter().GetResult();
            ok = removed; msg = removed ? "Eliminato" : "Id non trovato";
            break;
          }

        case "reset":
          {
            var all = repo.GetAllAsync().GetAwaiter().GetResult();
            var ids = new List<int>();
            foreach (var v in all) ids.Add(v.Id);
            count = repo.DeleteManyAsync(ids).GetAwaiter().GetResult();
            ok = true; msg = $"Cancellati {count} elementi";
            break;
          }

        default:
          {
            msg = "Comando sconosciuto. Usa: save|getall|delete|reset";
            break;
          }
      }

      da.SetData(0, ok);
      da.SetData(1, count);
      da.SetData(2, msg);
      da.SetDataList(3, varsOut);
    }

    private static List<int> ParseDeps(string csv)
    {
      var list = new List<int>();
      if (string.IsNullOrWhiteSpace(csv)) return list;

      var parts = csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var t in parts)
      {
        if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
          list.Add(id);
      }
      return list;
    }

    private static object ParseValue(string s)
    {
      if (string.IsNullOrEmpty(s)) return "";
      if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
      if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
      if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
      if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
      return s;
    }
  }
}
