// ProgesiMetadataOutComponent.cs
#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiMetadataOutComponent : GH_Component
  {
    public ProgesiMetadataOutComponent()
      : base("ProgesiMetadataOut", "MetOut",
             "Legge metadata per Hash (prioritario) o Id. LIVE (SQLite) se configurato, altrimenti mock.\n" +
             "Esempio rapido: Hash=mock-00000001",
             "Progesi", "Metadata")
    { }

    public override Guid ComponentGuid => new Guid("C0C0B7B7-5E9A-4A72-9C33-AC0E3A3B2C10");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.MetOut;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddTextParameter("Hash", "Hash", "Hash del metadata (es. 'mock-00000001').", GH_ParamAccess.item, "");
      p.AddIntegerParameter("Id", "Id", "Id del metadata (usato se Hash è vuoto).", GH_ParamAccess.item, 0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash.", GH_ParamAccess.item);
      p.AddTextParameter("By", "By", "Autore.", GH_ParamAccess.item);
      p.AddTextParameter("Refs", "Refs", "Riferimenti (uno per riga).", GH_ParamAccess.item);
      p.AddTextParameter("Snips", "Snips", "Snips (uno per riga).", GH_ParamAccess.item);
      p.AddTextParameter("LM", "LM", "LastModified (UTC ISO 8601).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito (OK / Non trovato / errore).", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true; string hash = ""; int id = 0;
      DA.GetData(0, ref run);
      DA.GetData(1, ref hash);
      DA.GetData(2, ref id);

      int oId = 0; string oHash = ""; string oBy = ""; string oRefs = ""; string oSnips = ""; string oLM = ""; string oInfo = "";

      if (!run) { Emit(DA, oId, oHash, oBy, oRefs, oSnips, oLM, "Idle"); return; }
      if (string.IsNullOrWhiteSpace(hash) && id <= 0) { Emit(DA, oId, oHash, oBy, oRefs, oSnips, oLM, "Input non valido: specificare Hash o Id>0"); return; }

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);
      if (repo == null) { Emit(DA, oId, oHash, oBy, oRefs, oSnips, oLM, hub); return; }

      object meta; string info;
      if (MetadataRepositoryCompatExtensions.TryGetByHashThenId(repo, hash ?? "", id, out meta, out info))
      {
        Map(meta, out oId, out oHash, out oBy, out oRefs, out oSnips, out oLM);
        oInfo = string.IsNullOrWhiteSpace(info) ? "OK" : info;
      }
      else { oInfo = string.IsNullOrWhiteSpace(info) ? "Non trovato" : info; }

      Emit(DA, oId, oHash, oBy, oRefs, oSnips, oLM, oInfo);
    }

    private static void Emit(IGH_DataAccess da, int id, string hash, string by, string refs, string snips, string lm, string info)
    {
      da.SetData(0, id);
      da.SetData(1, hash ?? ""); da.SetData(2, by ?? "");
      da.SetData(3, refs ?? ""); da.SetData(4, snips ?? "");
      da.SetData(5, lm ?? ""); da.SetData(6, info ?? "");
    }

    private static void Map(object meta, out int id, out string hash, out string by, out string refs, out string snips, out string lm)
    {
      id = GetInt(meta, "id");
      hash = GetString(meta, "hash");
      by = GetString(meta, "by");
      refs = Join(GetStrings(meta, "refs"));
      snips = Join(GetStrings(meta, "snips"));
      lm = GetString(meta, "lastModified");
    }

    private static string Join(IEnumerable<string> s) => s == null ? "" : string.Join(Environment.NewLine, s.Where(x => !string.IsNullOrEmpty(x)));
    private static int GetInt(object o, string n) { var v = GetVal(o, n); int z; if (v is int i) return i; return (v != null && int.TryParse(v.ToString(), out z)) ? z : 0; }
    private static string GetString(object o, string n) { var v = GetVal(o, n); return v?.ToString() ?? ""; }
    private static IEnumerable<string> GetStrings(object o, string n)
    {
      var v = GetVal(o, n); if (v == null) return Array.Empty<string>();
      if (v is IEnumerable<string> es) return es;
      if (v is IEnumerable ie) return ie.Cast<object>().Select(x => x?.ToString() ?? "");
      return Array.Empty<string>();
    }
    private static object GetVal(object o, string n)
    {
      if (o == null) return null;
      var t = o.GetType();
      var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi != null) return pi.GetValue(o, null);
      var fi = t.GetField(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (fi != null) return fi.GetValue(o);
      return null;
    }
  }
}
