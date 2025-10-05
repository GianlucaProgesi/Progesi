// ProgesiVariableInComponent.cs
#nullable disable
using System;
using System.Globalization;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiVariableInComponent : GH_Component
  {
    public ProgesiVariableInComponent()
      : base("ProgesiVariableIn", "VarIn",
             "Create/Update/Delete variable.\nLIVE (SQLite) se configurato, altrimenti mock/echo.\n" +
             "Dynamic out (Val): emette il Value con NickName uguale al Name.",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("C3E9F2B7-3B1B-4D28-B0D9-4F0C23B8B9C2");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.VarIn;

    // IN
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "Create | Update | Delete", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Id per Update/Delete.", GH_ParamAccess.item, 0);
      p.AddTextParameter("Name", "Name", "Nome variabile (es. LEN).", GH_ParamAccess.item, "");
      p.AddTextParameter("Value", "Value", "Valore (string/number).", GH_ParamAccess.item, "");
      p.AddTextParameter("Unit", "Unit", "Fattore numerico (double) applicato a Value se entrambi numerici; altrimenti ignorato.", GH_ParamAccess.item, "");
      p.AddTextParameter("By", "By", "Autore (es. GM).", GH_ParamAccess.item, "");
    }

    // OUT (0=Val dinamico, 1=Id, 2=Hash, 3=Info)
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Val", "Val", "Dynamic out: Value con NickName uguale al Name.", GH_ParamAccess.item);
      p.AddIntegerParameter("Id", "Id", "Id risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false; string act = "Create"; int id = 0; string name = ""; string val = ""; string unit = ""; string by = "";
      DA.GetData(0, ref run); DA.GetData(1, ref act); DA.GetData(2, ref id);
      DA.GetData(3, ref name); DA.GetData(4, ref val); DA.GetData(5, ref unit); DA.GetData(6, ref by);

      int outId = 0; string outHash = ""; string outInfo = "";

      if (!run) { Emit(DA, val, outId, outHash, "Idle", name); return; }

      bool isCreate = act.Equals("Create", StringComparison.OrdinalIgnoreCase);
      bool isUpdate = act.Equals("Update", StringComparison.OrdinalIgnoreCase);
      bool isDelete = act.Equals("Delete", StringComparison.OrdinalIgnoreCase);

      if (!(isCreate || isUpdate || isDelete)) { Emit(DA, val, outId, outHash, "Input non valido: Act", name); return; }
      if ((isUpdate || isDelete) && id <= 0) { Emit(DA, val, outId, outHash, "Input non valido: Id (>0)", name); return; }

      // Unit come fattore numerico opzionale: Value × Unit se ENTRAMBI numerici
      var inv = CultureInfo.InvariantCulture;
      double vFix, uFix;
      if (double.TryParse(val, NumberStyles.Any, inv, out vFix) &&
          double.TryParse(unit, NumberStyles.Any, inv, out uFix))
      {
        var combined = vFix * uFix;
        val = combined.ToString(inv);
        unit = ""; // fattore applicato → unit non serve più
      }

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);

      try
      {
        if (isDelete)
        {
          string info; bool ok = MetadataRepositoryCompatExtensions.TryDeleteVariable(repo, id, out info);
          outId = id > 0 ? id : 0; outHash = ""; outInfo = string.IsNullOrWhiteSpace(info) ? (ok ? "OK" : "Operazione non riuscita") : info;
          Emit(DA, val, outId, outHash, outInfo, name); return;
        }

        var payload = new VarPayload { id = id, name = name, value = val, unit = unit, by = by };
        object saved; string upInfo;
        bool upOk = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out saved, out upInfo);

        // >>> qui serve la doppia overload di ReadIf (int e string)!
        if (saved != null) { ReadIf(saved, "Id", ref outId); ReadIf(saved, "Hash", ref outHash); }
        else { outId = id > 0 ? id : 0; outHash = ""; }

        outInfo = string.IsNullOrWhiteSpace(upInfo) ? (upOk ? "OK" : "Operazione non riuscita") : upInfo;
        Emit(DA, val, outId, outHash, outInfo, name);
      }
      catch (Exception ex) { Emit(DA, val, outId, outHash, "Errore: " + ex.Message, name); }
    }

    // payload e helpers
    private sealed class VarPayload { public int id { get; set; } public string name { get; set; } public string value { get; set; } public string unit { get; set; } public string by { get; set; } }

    private static void ReadIf(object obj, string prop, ref int target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      int n; if (v is int i) target = i; else if (v != null && int.TryParse(v.ToString(), out n)) target = n;
    }

    // *** overload mancante nella tua versione: questa elimina il CS1503 ***
    private static void ReadIf(object obj, string prop, ref string target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      var s = (v == null ? "" : v.ToString() ?? "").Trim();
      if (s.Length > 0) target = s;
    }

    private void Emit(IGH_DataAccess DA, string val, int id, string hash, string info, string name)
    {
      // OUT0: Val dinamico + nickname aggiornato
      DA.SetData(0, val ?? "");
      var p = Params.Output[0];
      if (!string.IsNullOrWhiteSpace(name))
      {
        if (!string.Equals(p.NickName, name, StringComparison.Ordinal))
        {
          p.NickName = name;   // etichetta mostrata
          p.Name = name;   // tooltip e nome interno
          p.Description = "Dynamic value of '" + name + "'";
        }
      }
      DA.SetData(1, id);
      DA.SetData(2, hash ?? "");
      DA.SetData(3, info ?? "");
    }
  }
}
