using System;
using System.Collections.Generic;
using System.Globalization;
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

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddTextParameter("dbPath", "DB", "Percorso file .sqlite/.db", GH_ParamAccess.item);
            p.AddTextParameter("command", "Cmd", "save|getall|delete|reset", GH_ParamAccess.item);
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
            string dbPath = null, cmd = null, name = null, valStr = null, depsCsv = null;
            int id = 0;

            if (!da.GetData(0, ref dbPath)) return;
            if (!da.GetData(1, ref cmd)) return;
            da.GetData(2, ref id);
            da.GetData(3, ref name);
            da.GetData(4, ref valStr);
            da.GetData(5, ref depsCsv);

            var repo = new SqliteVariableRepository(dbPath);
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
                            varsOut.Add($"{v.Id}:{v.Name}={v.Value}");
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
                        // cancella tutti: fetch IDs e DeleteMany
                        var all = repo.GetAllAsync().GetAwaiter().GetResult();
                        var ids = new List<int>();
                        foreach (var v in all) ids.Add(v.Id);
                        count = repo.DeleteManyAsync(ids).GetAwaiter().GetResult();
                        ok = true; msg = $"Cancellati {count} elementi";
                        break;
                    }
                default:
                    msg = "Comando sconosciuto. Usa: save|getall|delete|reset";
                    break;
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
            foreach (var t in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    list.Add(id);
            return list;
        }

        private static object ParseValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            if (int.TryParse(s, out var i)) return i;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
            return s;
        }
    }
}
