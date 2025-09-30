// ProgesiMetadataOutComponent.cs
#nullable disable
using Grasshopper.Kernel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public class ProgesiMetadataOutComponent : GH_Component
  {
    // Messaggi standardizzati (micro-step 1.2)
    static class Msg
    {
      public const string Idle = "Idle";
      public const string Ok = "OK";
      public const string NotFound = "Non trovato";
      public const string NoRepo = "OK (nessun repo collegato)";
      public static string Invalid(string what) => $"Input non valido: {what}";
    }

    public ProgesiMetadataOutComponent()
        : base("ProgesiMetadataOut", "MetOut",
               "Legge metadata (priorità Hash → Id). Usa mock .json se presenti.",
               "Progesi", "Metadata")
    { }

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddTextParameter("Hash", "Hash", "Hash del metadata (prioritario).", GH_ParamAccess.item, "");
      p.AddIntegerParameter("Id", "Id", "Id del metadata (fallback se Hash vuoto).", GH_ParamAccess.item, 0);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Identificativo.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash code.", GH_ParamAccess.item);
      p.AddTextParameter("By", "By", "Autore.", GH_ParamAccess.item);
      p.AddTextParameter("Refs", "Refs", "Riferimenti (uno per riga).", GH_ParamAccess.item);
      p.AddTextParameter("Snips", "Snips", "Snip list (uno per riga).", GH_ParamAccess.item);
      p.AddTextParameter("LM", "LM", "LastModified (ISO).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Messaggio esito.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = true; string hash = ""; int id = 0;
      da.GetData(0, ref run);
      da.GetData(1, ref hash);
      da.GetData(2, ref id);

      // default outs
      int oId = 0;
      string oHash = "", oBy = "", oRefs = "", oSnips = "", oLM = "", oInfo;

      if (!run)
      {
        oInfo = Msg.Idle;
        Emit(da, oId, oHash, oBy, oRefs, oSnips, oLM, oInfo);
        return;
      }

      // Convalida minima: serve Hash oppure Id>0
      if (string.IsNullOrWhiteSpace(hash) && id <= 0)
      {
        oInfo = Msg.Invalid("specificare Hash oppure Id>0");
        Emit(da, oId, oHash, oBy, oRefs, oSnips, oLM, oInfo);
        return;
      }

      object repo; string repoInfo;
      ServiceHub.TryGetMetadataRepository(out repo, out repoInfo);
      if (repo == null)
      {
        oInfo = string.IsNullOrWhiteSpace(repoInfo) ? Msg.NoRepo : repoInfo;
        Emit(da, oId, oHash, oBy, oRefs, oSnips, oLM, oInfo);
        return;
      }

      object meta; string info;
      if (MetadataRepositoryCompatExtensions.TryGetByHashThenId(repo, hash ?? "", id, out meta, out info))
      {
        MapMeta(meta, out oId, out oHash, out oBy, out oRefs, out oSnips, out oLM);
        oInfo = Msg.Ok;
      }
      else
      {
        oInfo = string.IsNullOrWhiteSpace(info) ? Msg.NotFound : info;
      }

      Emit(da, oId, oHash, oBy, oRefs, oSnips, oLM, oInfo);
    }

    private static void Emit(IGH_DataAccess da, int id, string hash, string by, string refs, string snips, string lm, string info)
    {
      da.SetData(0, id);
      da.SetData(1, hash ?? "");
      da.SetData(2, by ?? "");
      da.SetData(3, refs ?? "");
      da.SetData(4, snips ?? "");
      da.SetData(5, lm ?? "");
      da.SetData(6, info ?? "");
    }

    private static void MapMeta(object meta, out int id, out string hash, out string by, out string refs, out string snips, out string lm)
    {
      id = GetInt(meta, "id");
      hash = GetString(meta, "hash");
      by = GetString(meta, "by");
      refs = JoinLines(GetStrings(meta, "refs"));
      snips = JoinLines(GetStrings(meta, "snips"));
      lm = GetString(meta, "lastModified");
    }

    private static string JoinLines(IEnumerable<string> lines)
      => (lines == null) ? "" : string.Join(Environment.NewLine, lines.Where(s => !string.IsNullOrEmpty(s)));

    private static int GetInt(object obj, string name)
    {
      var v = GetValue(obj, name);
      if (v == null) return 0;
      if (v is int i) return i;
      int.TryParse(v.ToString(), out var n);
      return n;
    }

    private static string GetString(object obj, string name)
    {
      var v = GetValue(obj, name);
      return v?.ToString() ?? "";
    }

    private static IEnumerable<string> GetStrings(object obj, string name)
    {
      var v = GetValue(obj, name);
      if (v == null) return Array.Empty<string>();

      if (v is IEnumerable<string> es) return es;
      if (v is IEnumerable e) return e.Cast<object>().Select(x => x?.ToString() ?? "");
      return Array.Empty<string>();
    }

    private static object GetValue(object obj, string name)
    {
      if (obj == null) return null;

      // dynamic/IDictionary
      if (obj is IDictionary dict && dict.Contains(name)) return dict[name];

      // proprietà pubbliche
      var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (prop != null) return prop.GetValue(obj);

      // campi pubblici
      var fld = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (fld != null) return fld.GetValue(obj);

      return null;
    }

    public override Guid ComponentGuid => new Guid("C0C0B7B7-5E9A-4A72-9C33-AC0E3A3B2C10");
  }
}
