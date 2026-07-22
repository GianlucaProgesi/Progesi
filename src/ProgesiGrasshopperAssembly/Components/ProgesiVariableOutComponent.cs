// ProgesiVariableOutComponent.cs
#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiVariableOutComponent : GH_Component
  {
    public ProgesiVariableOutComponent()
      : base("ProgesiVariableOut", "VarOut",
             "Legge variabili dal repository Rhino usando Hash/Id (liste/alberi) e produce output completi (tree).",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("9B3A7A4D-7A4D-4553-8F16-5A84D6A9F5A9");
    protected override System.Drawing.Bitmap Icon => ProgesiGrasshopperAssembly.Infrastructure.ProgesiIcons.VarOut;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddTextParameter("Hash", "Hash", "Hash (digest o riepilogo umano con 'ID:' oppure vuoto).", GH_ParamAccess.tree);
      // Hash opzionale
      Params.Input[Params.Input.Count - 1].Optional = true;

      p.AddIntegerParameter("Id", "Id", "Id (usato se Hash è vuoto).", GH_ParamAccess.tree);
      // Id opzionale
      Params.Input[Params.Input.Count - 1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Val", "Val", "Dynamic: Value con NickName uguale al Name (se singolo elemento).", GH_ParamAccess.tree);
      p.AddIntegerParameter("Id", "Id", "Id.", GH_ParamAccess.tree);
      p.AddTextParameter("Hash", "Hash", "Riepilogo umano: ID/NAME/VALC/BY/MID/DEP/ASS.", GH_ParamAccess.tree);
      p.AddTextParameter("Name", "Name", "Nome.", GH_ParamAccess.tree);
      p.AddTextParameter("Value", "Value", "Valore 'grezzo'.", GH_ParamAccess.tree);
      p.AddTextParameter("ValC", "ValC", "Valore canonicalizzato (per hash).", GH_ParamAccess.tree);
      p.AddTextParameter("By", "By", "Autore (se non persistito → '-').", GH_ParamAccess.tree);
      p.AddIntegerParameter("MetaId", "MId", "Metadata Id (0 se assente).", GH_ParamAccess.tree);
      p.AddIntegerParameter("Depends", "Dep", "Dipendenze come lista: per ciascun item: path {p;i}.", GH_ParamAccess.tree);
      p.AddBooleanParameter("Assumption", "Ass", "IsAssumption.", GH_ParamAccess.tree);
      p.AddTextParameter("LM", "LM", "LastModified (ISO).", GH_ParamAccess.tree);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.tree);
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

      var allPaths = new SortedSet<GH_Path>(Comparer<GH_Path>.Create((a, b) => a.CompareTo(b)));
      foreach (var p in hashIn.Paths) allPaths.Add(p);
      foreach (var p in idIn.Paths) allPaths.Add(p);
      if (allPaths.Count == 0) allPaths.Add(new GH_Path(0));

      var tVal = new GH_Structure<GH_String>();
      var tId = new GH_Structure<GH_Integer>();
      var tHash = new GH_Structure<GH_String>();
      var tName = new GH_Structure<GH_String>();
      var tValue = new GH_Structure<GH_String>();
      var tValC = new GH_Structure<GH_String>();
      var tBy = new GH_Structure<GH_String>();
      var tMId = new GH_Structure<GH_Integer>();
      var tDep = new GH_Structure<GH_Integer>();
      var tAss = new GH_Structure<GH_Boolean>();
      var tLM = new GH_Structure<GH_String>();
      var tInfo = new GH_Structure<GH_String>();

      object repo; string hub; ServiceHub.TryGetMetadataRepository(out repo, out hub);

      int totalItems = 0;
      string firstName = null;

      foreach (var p in allPaths)
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
          bool ok = MetadataRepositoryCompatExtensions.TryGetVariableByHashThenId(repo, hash, id, out obj, out info);

          string name = "", value = "", valc = "", by = "-", lm = "";
          int outId = 0, mid = 0;
          bool ass = false;
          int[] deps = Array.Empty<int>();
          string summary = "";

          if (ok && obj != null)
          {
            ReadIf(obj, "Id", ref outId);
            ReadIf(obj, "Name", ref name);
            ReadIf(obj, "Value", ref value);
            ReadIf(obj, "ValueCanonical", ref valc);
            ReadIf(obj, "By", ref by, "-");
            ReadIf(obj, "LastModified", ref lm);
            ReadIf(obj, "MetaId", ref mid);
            ass = ReadBool(obj, "IsAssumption");
            deps = ReadDepends(obj);
            ReadIf(obj, "Summary", ref summary);
          }

          if (string.IsNullOrWhiteSpace(summary))
          {
            var nameN = (name ?? "").Trim().ToUpperInvariant();
            var byN = string.IsNullOrWhiteSpace(by) ? "-" : (by ?? "").Trim().ToUpperInvariant();
            var depStr = (deps == null || deps.Length == 0) ? "-" : string.Join(",", deps);
            var assN = ass ? "1" : "0";
            summary = $"ID:{outId} | NAME:{nameN} | VALC:{(valc ?? "")} | BY:{byN} | MID:{(mid > 0 ? mid.ToString() : "-")} | DEP:[{depStr}] | ASS:{assN}";
          }

          var path = p;
          tVal.Append(new GH_String(value ?? ""), path);
          tId.Append(new GH_Integer(outId), path);
          tHash.Append(new GH_String(summary), path);
          tName.Append(new GH_String(name ?? ""), path);
          tValue.Append(new GH_String(value ?? ""), path);
          tValC.Append(new GH_String(valc ?? ""), path);
          tBy.Append(new GH_String(string.IsNullOrWhiteSpace(by) ? "-" : by), path);
          tMId.Append(new GH_Integer(mid), path);
          tAss.Append(new GH_Boolean(ass), path);
          tLM.Append(new GH_String(lm ?? ""), path);

          var depPath = new GH_Path(path); depPath = depPath.AppendElement(i);
          if (deps != null && deps.Length > 0)
            foreach (var d in deps) tDep.Append(new GH_Integer(d), depPath);
          else
            tDep.Append(new GH_Integer(0), depPath);

          var pref = ok ? "OK" : (string.IsNullOrWhiteSpace(info) ? "Errore" : info);
          tInfo.Append(new GH_String(string.IsNullOrWhiteSpace(summary) ? pref : (pref + " | " + summary)), path);

          if (firstName == null && !string.IsNullOrWhiteSpace(name)) firstName = name;
          totalItems++;
        }
      }

      DA.SetDataTree(0, tVal);
      DA.SetDataTree(1, tId);
      DA.SetDataTree(2, tHash);
      DA.SetDataTree(3, tName);
      DA.SetDataTree(4, tValue);
      DA.SetDataTree(5, tValC);
      DA.SetDataTree(6, tBy);
      DA.SetDataTree(7, tMId);
      DA.SetDataTree(8, tDep);
      DA.SetDataTree(9, tAss);
      DA.SetDataTree(10, tLM);
      DA.SetDataTree(11, tInfo);

      var p0 = Params.Output[0];
      if (totalItems == 1 && !string.IsNullOrWhiteSpace(firstName))
      {
        if (!string.Equals(p0.NickName, firstName, StringComparison.Ordinal))
        {
          p0.NickName = firstName;
          p0.Name = firstName;
          p0.Description = "Dynamic value of '" + firstName + "'";
        }
      }
      else
      {
        if (!string.Equals(p0.NickName, "Val", StringComparison.Ordinal))
        {
          p0.NickName = "Val";
          p0.Name = "Val";
          p0.Description = "Dynamic value (multi)";
        }
      }
    }

    // -------- GH input helpers (CastTo<T>(out …)) --------
    private static string ReadGhString(IList list, int i)
    {
      if (list == null || i < 0 || i >= list.Count) return "";
      if (list[i] is GH_String s) return s.Value ?? "";

      if (list[i] is IGH_Goo goo)
      {
        string val;
        if (goo.CastTo(out val) && val != null) return val;

        object obj;
        if (goo.CastTo(out obj) && obj != null) return obj.ToString();
      }
      return list[i]?.ToString() ?? "";
    }

    private static int ReadGhInt(IList list, int i)
    {
      if (list == null || i < 0 || i >= list.Count) return 0;
      if (list[i] is GH_Integer gi) return gi.Value;

      if (list[i] is IGH_Goo goo)
      {
        int n;
        if (goo.CastTo(out n)) return n;

        double d;
        if (goo.CastTo(out d)) return (int)Math.Round(d);

        string s;
        if (goo.CastTo(out s) && int.TryParse(s, out n)) return n;
      }
      return 0;
    }

    // -------- reflection helpers --------
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
    private static bool ReadBool(object obj, string prop)
    {
      if (obj == null) return false;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return false;
      var v = pi.GetValue(obj, null);
      if (v == null) return false;
      if (v is bool b) return b;
      bool bb; return bool.TryParse(v.ToString(), out bb) && bb;
    }
    private static int[] ReadDepends(object obj)
    {
      if (obj == null) return Array.Empty<int>();
      var pi = obj.GetType().GetProperty("Depends", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return Array.Empty<int>();
      var v = pi.GetValue(obj, null);
      if (v == null) return Array.Empty<int>();

      if (v is IEnumerable<int> ints) return ints.ToArray();
      if (v is System.Collections.IEnumerable en)
      {
        var list = new List<int>();
        foreach (var o in en)
        {
          if (o == null) continue;
          if (o is int ii) { list.Add(ii); continue; }
          int n; if (int.TryParse(o.ToString(), out n)) list.Add(n);
        }
        return list.ToArray();
      }
      return Array.Empty<int>();
    }
  }
}
