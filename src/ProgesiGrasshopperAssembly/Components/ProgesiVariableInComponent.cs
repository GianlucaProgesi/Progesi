// ProgesiVariableInComponent.cs
#nullable disable
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiCore;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiVariableInComponent : GH_Component
  {
    public ProgesiVariableInComponent()
      : base("ProgesiVariableIn", "VarIn",
             "Create/Update/Delete variable (Rhino repository).\n" +
             "Dynamic out (Val): emette il Value con NickName uguale al Name.",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("A7F6E146-2D5D-4E5A-9E5E-3E0B4CF5B2D4");
    protected override System.Drawing.Bitmap Icon => ProgesiGrasshopperAssembly.Infrastructure.ProgesiIcons.VarIn;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "Create | Update | Delete", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Id per Update/Delete.", GH_ParamAccess.item, 0);
      p.AddTextParameter("Name", "Name", "Nome variabile (es. LEN).", GH_ParamAccess.item, "");
      p.AddTextParameter("Value", "Value", "Valore (string/number).", GH_ParamAccess.item, "");
      p.AddTextParameter("Unit", "Unit", "Fattore numerico (double) applicato a Value se ENTRAMBI numerici; altrimenti ignorato.", GH_ParamAccess.item, "");
      p.AddTextParameter("By", "By", "Autore (es. GM).", GH_ParamAccess.item, "");

      p.AddBooleanParameter("Assumption", "Ass", "TRUE se il valore è un’assunzione (IsAssumption).", GH_ParamAccess.item, false);
      p.AddTextParameter("MId", "MId", "Id numerico della Metadata o il suo Hash.", GH_ParamAccess.item, "");
      p.AddIntegerParameter("Depends", "Dep", "Lista Id da cui dipende (DependsFrom). Se vuoto → variabile indipendente.", GH_ParamAccess.list);
      Params.Input[Params.Input.Count - 1].Optional = true; // Dep opzionale
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Val", "Val", "Dynamic out: Value con NickName uguale al Name.", GH_ParamAccess.item);
      p.AddIntegerParameter("Id", "Id", "Id risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Riepilogo hash umano (ID/NAME/VALC/BY/MID/DEP/ASS).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
    }

    // IDs già accettati per scrittura nella soluzione corrente (tutti i branch del tree).
    // Serve a respingere branch in conflitto che ripetono lo stesso Id esplicito senza overwrite silenzioso.
    private readonly HashSet<int> _seenWriteIds = new HashSet<int>();

    protected override void BeforeSolveInstance()
    {
      _seenWriteIds.Clear();
      base.BeforeSolveInstance();
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false; string act = "Create"; int id = 0; string name = ""; string val = ""; string unit = ""; string by = "";
      bool isAss = false; string mid = "";
      var depends = new List<int>();

      DA.GetData(0, ref run); DA.GetData(1, ref act); DA.GetData(2, ref id);
      DA.GetData(3, ref name); DA.GetData(4, ref val); DA.GetData(5, ref unit); DA.GetData(6, ref by);
      DA.GetData(7, ref isAss); DA.GetData(8, ref mid);
      if (!DA.GetDataList(9, depends)) depends.Clear();

      int outId = 0; string outHash = ""; string outInfo = "";

      if (!run) { Emit(DA, val, outId, outHash, "Idle", name); return; }

      bool isCreate = act.Equals("Create", StringComparison.OrdinalIgnoreCase);
      bool isUpdate = act.Equals("Update", StringComparison.OrdinalIgnoreCase);
      bool isDelete = act.Equals("Delete", StringComparison.OrdinalIgnoreCase);

      if (!(isCreate || isUpdate || isDelete)) { Fail(DA, val, outId, outHash, "Input non valido: Act", name); return; }
      if ((isUpdate || isDelete) && id <= 0) { Fail(DA, val, outId, outHash, "Input non valido: Id (>0)", name); return; }

      // Tree overwrite safety: lo stesso Id esplicito non può comparire in più branch della stessa soluzione.
      if ((isCreate || isUpdate) && id > 0 && !_seenWriteIds.Add(id))
      {
        Fail(DA, val, outId, outHash, $"Conflitto tree: Id {id} ripetuto in più branch. Nessun overwrite silenzioso.", name);
        return;
      }

      // Unit come fattore numerico opzionale: Value × Unit se ENTRAMBI numerici
      var inv = CultureInfo.InvariantCulture;
      if (double.TryParse(val, NumberStyles.Any, inv, out var vFix) &&
          double.TryParse(unit, NumberStyles.Any, inv, out var uFix))
      {
        var combined = vFix * uFix;
        val = combined.ToString(inv);
        unit = "";
      }

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);

      try
      {
        if (isDelete)
        {
          string info; bool ok = MetadataRepositoryCompatExtensions.TryDeleteVariable(repo, id, out info);
          outId = id > 0 ? id : 0; outHash = ""; outInfo = string.IsNullOrWhiteSpace(info) ? (ok ? "OK" : "Operazione non riuscita") : info;
          if (!ok) { Fail(DA, val, outId, outHash, outInfo, name); } else { Emit(DA, val, outId, outHash, outInfo, name); }
          return;
        }

        var payload = new VarPayload
        {
          id = id,
          act = act ?? "Create",
          allowIdReassign = isCreate && IsTreeOrBatchInput(),
          name = name ?? "",
          value = val ?? "",
          unit = unit ?? "",
          by = by ?? "",
          isAssumption = isAss,
          mid = mid ?? "",
          depends = depends?.ToArray() ?? Array.Empty<int>()
        };

        object saved; string upInfo;
        bool upOk = MetadataRepositoryCompatExtensions.TryUpsertVariable(repo, payload, out saved, out upInfo);

        // Out principali
        if (saved != null) { ReadIf(saved, "Id", ref outId); ReadIf(saved, "Hash", ref outHash); }
        else { outId = id > 0 ? id : 0; outHash = ""; }

        // Info “ricca”: se Summary presente, prefissiamo con OK/errore
        string summary = ""; ReadIf(saved, "Summary", ref summary);
        var prefix = string.IsNullOrWhiteSpace(upInfo) ? (upOk ? "OK" : "Operazione non riuscita") : upInfo;
        outInfo = string.IsNullOrWhiteSpace(summary) ? prefix : $"{prefix} | {summary}";

        if (!upOk) { Fail(DA, val, outId, outHash, outInfo, name); return; }
        Emit(DA, val, outId, outHash, outInfo, name);
      }
      catch (Exception ex) { Fail(DA, val, outId, outHash, "Errore: " + ex.Message, name); }
    }

    // Più path nella stessa soluzione → batch/tree (Test-17: Create con Id esistente → riassegnazione, non overwrite).
    private bool IsTreeOrBatchInput()
    {
      foreach (var param in Params.Input)
      {
        if (param?.VolatileData != null && param.VolatileData.PathCount > 1)
          return true;
      }
      return false;
    }

    private sealed class VarPayload
    {
      public int id { get; set; }
      public string act { get; set; }
      public bool allowIdReassign { get; set; }
      public string name { get; set; }
      public string value { get; set; }
      public string unit { get; set; }
      public string by { get; set; }
      public bool isAssumption { get; set; }
      public string mid { get; set; }
      public int[] depends { get; set; }
    }

    private static void ReadIf(object obj, string prop, ref int target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      if (v == null) return;
      if (int.TryParse(v.ToString(), out var n)) target = n;
    }
    private static void ReadIf(object obj, string prop, ref string target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      target = v == null ? "" : v.ToString();
    }

    // Esito bloccante: errore rosso sul componente + Info testuale, senza overwrite silenzioso.
    private void Fail(IGH_DataAccess DA, string val, int id, string hash, string info, string name)
    {
      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, string.IsNullOrWhiteSpace(info) ? "Operazione non riuscita" : info);
      Emit(DA, val, id, hash, info, name);
    }

    private void Emit(IGH_DataAccess DA, string val, int id, string hash, string info, string name)
    {
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
      DA.SetData(0, val ?? "");
      DA.SetData(1, id);
      DA.SetData(2, hash ?? "");
      DA.SetData(3, info ?? "");
    }
  }
}
