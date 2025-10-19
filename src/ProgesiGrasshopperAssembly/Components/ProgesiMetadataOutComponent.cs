// ProgesiMetadataOutComponent.cs
#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiMetadataOutComponent : GH_Component
  {
    public ProgesiMetadataOutComponent()
      : base("ProgesiMetadataOut", "MetaOut",
             "Legge metadata dal repository Rhino usando Hash/Id (liste/alberi) e produce output completi (tree).",
             "Progesi", "Metadata")
    { }

    public override Guid ComponentGuid => new Guid("C6E8B6C5-8B53-4E3B-8C5D-6B0AF7A4E6B5");
    // icona (coerente con MetIn): presumibilmente ProgesiIcons.MetOut
    protected override System.Drawing.Bitmap Icon => ProgesiGrasshopperAssembly.Infrastructure.ProgesiIcons.MetOut;

    // IN: Run, Hash (tree), Id (tree) – ENTRAMBI opzionali; priorità ad Hash
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);

      p.AddTextParameter("Hash", "Hash", "Hash (digest o riepilogo umano con 'ID:' oppure vuoto).", GH_ParamAccess.tree);
      Params.Input[Params.Input.Count - 1].Optional = true;

      p.AddIntegerParameter("Id", "Id", "Id (usato se Hash è vuoto).", GH_ParamAccess.tree);
      Params.Input[Params.Input.Count - 1].Optional = true;
    }

    // OUT (tutti TREE; Refs/Snips nei sotto-path {p;i})
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id.", GH_ParamAccess.tree);                           // 0
      p.AddTextParameter("Hash", "Hash", "Riepilogo umano: ID/BY/DESC.", GH_ParamAccess.tree); // 1
      p.AddTextParameter("By", "By", "Autore (UPPER, '-' se vuoto).", GH_ParamAccess.tree);    // 2
      p.AddTextParameter("Description", "Desc", "Descrizione.", GH_ParamAccess.tree);          // 3
      p.AddTextParameter("Refs", "Refs", "Riferimenti normalizzati (branch {p;i}).", GH_ParamAccess.tree); // 4
      p.AddTextParameter("Snips", "Snips", "Snip summary (branch {p;i}).", GH_ParamAccess.tree);            // 5
      p.AddTextParameter("LM", "LM", "LastModified (ISO).", GH_ParamAccess.tree);              // 6
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.tree);           // 7
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true; DA.GetData(0, ref run);
      if (!run)
      {
        for (int i = 0; i < Params.Output.Count; i++)
          DA.SetDataTree(i, new GH_Structure<IGH_Goo>());
        return;
      }

      var hashIn = new GH_Structure<GH_String>();
      var idIn = new GH_Structure<GH_Integer>();
      DA.GetDataTree(1, out hashIn);
      DA.GetDataTree(2, out idIn);

      var paths = new SortedSet<GH_Path>(Comparer<GH_Path>.Create((a, b) => a.CompareTo(b)));
      foreach (var p in hashIn.Paths) paths.Add(p);
      foreach (var p in idIn.Paths) paths.Add(p);
      if (paths.Count == 0) paths.Add(new GH_Path(0));

      var tId = new GH_Structure<GH_Integer>();
      var tHash = new GH_Structure<GH_String>();
      var tBy = new GH_Structure<GH_String>();
      var tDesc = new GH_Structure<GH_String>();
      var tRefs = new GH_Structure<GH_String>();
      var tSnip = new GH_Structure<GH_String>();
      var tLM = new GH_Structure<GH_String>();
      var tInfo = new GH_Structure<GH_String>();

      object repo; string hub; ServiceHub.TryGetMetadataRepository(out repo, out hub);

      foreach (var p in paths)
      {
        IList hs = hashIn.get_Branch(p);
        IList is_ = idIn.get_Branch(p);
        int n = Math.Max(hs?.Count ?? 0, is_?.Count ?? 0);
        if (n == 0) n = 1;

        for (int i = 0; i < n; i++)
        {
          string hash = ReadGhString(hs, i);
          int id = ReadGhInt(is_, i);

          string info;
          object obj;
          bool ok = MetadataRepositoryCompatExtensions.TryGetByHashThenId(repo, hash, id, out obj, out info);

          int outId = 0;
          string sum = "", by = "-", desc = "", lm = "";
          string[] refs = Array.Empty<string>();
          string[] snips = Array.Empty<string>();

          if (ok && obj != null)
          {
            ReadIf(obj, "Id", ref outId);
            ReadIf(obj, "Hash", ref sum);
            ReadIf(obj, "Summary", ref sum);
            ReadIf(obj, "By", ref by, "-");
            ReadIf(obj, "Description", ref desc, "");
            ReadIf(obj, "LastModified", ref lm, "");
            refs = ReadStrings(obj, "Refs");
            snips = ReadStrings(obj, "Snips");
          }

          var path = p;
          tId.Append(new GH_Integer(outId), path);
          tHash.Append(new GH_String(sum ?? ""), path);
          tBy.Append(new GH_String(string.IsNullOrWhiteSpace(by) ? "-" : by), path);
          tDesc.Append(new GH_String(desc ?? ""), path);
          tLM.Append(new GH_String(lm ?? ""), path);

          // sotto-branch {p;i}
          var sub = new GH_Path(path); sub = sub.AppendElement(i);
          if (refs != null && refs.Length > 0)
            foreach (var r in refs) tRefs.Append(new GH_String(r ?? ""), sub);
          else
            tRefs.Append(new GH_String(""), sub);

          if (snips != null && snips.Length > 0)
            foreach (var s in snips) tSnip.Append(new GH_String(s ?? ""), sub);
          else
            tSnip.Append(new GH_String(""), sub);

          var pref = ok ? "OK" : (string.IsNullOrWhiteSpace(info) ? "Errore" : info);
          tInfo.Append(new GH_String(string.IsNullOrWhiteSpace(sum) ? pref : (pref + " | " + sum)), path);
        }
      }

      DA.SetDataTree(0, tId);
      DA.SetDataTree(1, tHash);
      DA.SetDataTree(2, tBy);
      DA.SetDataTree(3, tDesc);
      DA.SetDataTree(4, tRefs);
      DA.SetDataTree(5, tSnip);
      DA.SetDataTree(6, tLM);
      DA.SetDataTree(7, tInfo);
    }

    // ---- GH input helpers ----
    private static string ReadGhString(IList list, int i)
    {
      if (list == null || i < 0 || i >= list.Count) return "";
      if (list[i] is GH_String s) return s.Value ?? "";

      if (list[i] is IGH_Goo goo)
      {
        string val; if (goo.CastTo(out val) && val != null) return val;
        object obj; if (goo.CastTo(out obj) && obj != null) return obj.ToString();
      }
      return list[i]?.ToString() ?? "";
    }

    private static int ReadGhInt(IList list, int i)
    {
      if (list == null || i < 0 || i >= list.Count) return 0;
      if (list[i] is GH_Integer gi) return gi.Value;

      if (list[i] is IGH_Goo goo)
      {
        int n; if (goo.CastTo(out n)) return n;
        double d; if (goo.CastTo(out d)) return (int)Math.Round(d);
        string s; if (goo.CastTo(out s) && int.TryParse(s, out n)) return n;
      }
      return 0;
    }

    // ---- reflection helpers ----
    private static void ReadIf(object obj, string prop, ref int target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      if (v == null) return;
      int n; if (int.TryParse(v.ToString(), out n)) target = n;
    }

    private static void ReadIf(object obj, string prop, ref string target, string fallback = "")
    {
      if (obj == null) { target = fallback; return; }
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) { target = fallback; return; }
      var v = pi.GetValue(obj, null);
      target = v == null ? fallback : v.ToString();
    }

    private static string[] ReadStrings(object obj, string prop)
    {
      if (obj == null) return Array.Empty<string>();
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return Array.Empty<string>();
      var v = pi.GetValue(obj, null);
      if (v == null) return Array.Empty<string>();

      if (v is IEnumerable<string> ss) return ss.ToArray();
      if (v is System.Collections.IEnumerable en)
      {
        var list = new List<string>();
        foreach (var o in en) list.Add(o?.ToString() ?? "");
        return list.ToArray();
      }
      return Array.Empty<string>();
    }
  }
}
