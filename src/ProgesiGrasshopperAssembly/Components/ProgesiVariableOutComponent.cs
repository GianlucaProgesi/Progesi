// ProgesiVariableOutComponent.cs
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
  public sealed class ProgesiVariableOutComponent : GH_Component
  {
    public ProgesiVariableOutComponent()
      : base("ProgesiVariableOut", "VarOut",
             "Legge variabile per Hash (prioritario) o Id. LIVE (SQLite) se configurato.\n" +
             "Dynamic out (Val): emette il Value con NickName uguale al Name.",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("E6E25C83-9C8C-4C3D-8F7A-37C8C1E7D9AA");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.VarOut;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddTextParameter("Hash", "Hash", "Hash della variabile.", GH_ParamAccess.item, "");
      p.AddIntegerParameter("Id", "Id", "Id (usato se Hash è vuoto).", GH_ParamAccess.item, 0);
    }

    // OUT (0=Val dinamico, poi i campi classici)
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Val", "Val", "Dynamic out: Value con NickName uguale al Name.", GH_ParamAccess.item);
      p.AddIntegerParameter("Id", "Id", "Id.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash.", GH_ParamAccess.item);
      p.AddTextParameter("Name", "Name", "Nome.", GH_ParamAccess.item);
      p.AddTextParameter("Value", "Value", "Valore.", GH_ParamAccess.item);
      p.AddTextParameter("Unit", "Unit", "Unità.", GH_ParamAccess.item);
      p.AddTextParameter("By", "By", "Autore.", GH_ParamAccess.item);
      p.AddTextParameter("LM", "LM", "LastModified (UTC ISO).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true; string hash = ""; int id = 0;
      DA.GetData(0, ref run); DA.GetData(1, ref hash); DA.GetData(2, ref id);

      int oId = 0; string oHash = ""; string oName = ""; string oVal = ""; string oUnit = ""; string oBy = ""; string oLm = ""; string oInfo = "";

      if (!run) { Emit(DA, oVal, oId, oHash, oName, oUnit, oBy, oLm, "Idle"); return; }
      if (string.IsNullOrWhiteSpace(hash) && id <= 0) { Emit(DA, oVal, oId, oHash, oName, oUnit, oBy, oLm, "Input non valido: Hash o Id>0"); return; }

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);
      if (repo == null) { Emit(DA, oVal, oId, oHash, oName, oUnit, oBy, oLm, hub); return; }

      object dto; string info;
      if (MetadataRepositoryCompatExtensions.TryGetVariableByHashThenId(repo, hash ?? "", id, out dto, out info))
      {
        Map(dto, out oId, out oHash, out oName, out oVal, out oUnit, out oBy, out oLm);
        oInfo = string.IsNullOrWhiteSpace(info) ? "OK" : info;
      }
      else { oInfo = string.IsNullOrWhiteSpace(info) ? "Non trovato" : info; }

      Emit(DA, oVal, oId, oHash, oName, oUnit, oBy, oLm, oInfo);
    }

    private void Emit(IGH_DataAccess da, string val, int id, string hash, string name, string unit, string by, string lm, string info)
    {
      // OUT0: dinamico (Val) + nickname aggiornato
      da.SetData(0, val ?? "");
      var p = Params.Output[0];
      if (!string.IsNullOrWhiteSpace(name))
      {
        if (!string.Equals(p.NickName, name, StringComparison.Ordinal))
        {
          p.NickName = name;
          p.Name = name;
          p.Description = "Dynamic value of '" + name + "'";
        }
      }

      da.SetData(1, id);
      da.SetData(2, hash ?? "");
      da.SetData(3, name ?? "");
      da.SetData(4, val ?? "");
      da.SetData(5, unit ?? "");
      da.SetData(6, by ?? "");
      da.SetData(7, lm ?? "");
      da.SetData(8, info ?? "");
    }

    private static void Map(object o, out int id, out string hash, out string name, out string value, out string unit, out string by, out string lm)
    {
      id = GetInt(o, "id"); hash = GetString(o, "hash");
      name = GetString(o, "name"); value = GetString(o, "value");
      unit = GetString(o, "unit"); by = GetString(o, "by");
      lm = GetString(o, "lastModified");
    }

    private static int GetInt(object o, string n) { var v = GetVal(o, n); int z; if (v is int i) return i; return (v != null && int.TryParse(v.ToString(), out z)) ? z : 0; }
    private static string GetString(object o, string n) { var v = GetVal(o, n); return v?.ToString() ?? ""; }
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
