#nullable disable
using System;
using System.Diagnostics;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiMetadataInComponent : GH_Component
  {
    public ProgesiMetadataInComponent()
      : base("ProgesiMetadataIn", "MetIn",
             "Create/Update/Delete metadata.\n" +
             "LIVE (SQLite) se PROGESI_LIVE_ON=1 e PROGESI_LIVE_DB punta ad un file .db; altrimenti mock/echo.",
             "Progesi", "Metadata")
    { }

    public override Guid ComponentGuid => new Guid("9B6AA5E3-6C3B-4C0E-B1B3-86E0B7F1F8C7");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.MetIn;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "Create | Update | Delete", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Id (Update/Delete).", GH_ParamAccess.item, 0);
      p.AddTextParameter("By", "By", "Autore (es. 'GM').", GH_ParamAccess.item, "");
      p.AddTextParameter("Info", "Info", "Descrizione breve.", GH_ParamAccess.item, "");
      p.AddTextParameter("Ref", "Ref", "Riferimento (URL http/https o file esistente).", GH_ParamAccess.item, "");
      p.AddTextParameter("Snip", "Snip", "Snip normalizzato (snip:*/data-url/base64/path/url).", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica (OK / invalid ref/snip / errore).", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false; string act = "Create"; int inId = 0; string by = ""; string descr = ""; string rfIn = ""; string snIn = "";
      DA.GetData(0, ref run);
      DA.GetData(1, ref act);
      DA.GetData(2, ref inId);
      DA.GetData(3, ref by);
      DA.GetData(4, ref descr);
      DA.GetData(5, ref rfIn);
      DA.GetData(6, ref snIn);

      int outId = 0; string outHash = ""; string outInfo = "";

      if (!run) { Emit(DA, outId, outHash, "Idle"); return; }

      bool isCreate = act.Equals("Create", StringComparison.OrdinalIgnoreCase);
      bool isUpdate = act.Equals("Update", StringComparison.OrdinalIgnoreCase);
      bool isDelete = act.Equals("Delete", StringComparison.OrdinalIgnoreCase);

      if (!(isCreate || isUpdate || isDelete)) { Emit(DA, outId, outHash, "Input non valido: Act (Create|Update|Delete)"); return; }
      if ((isUpdate || isDelete) && inId <= 0) { Emit(DA, outId, outHash, "Input non valido: Id (>0) richiesto per Update/Delete)"); return; }

      // --- Normalizzazione/validazione Ref & Snip
      string rfNorm = "", refWhy = "";
      if (!string.IsNullOrWhiteSpace(rfIn))
      {
        if (!SnipHelpers.TryNormalizeRef(rfIn, out rfNorm, out refWhy))
        {
          Emit(DA, outId, outHash, "Invalid Ref: " + refWhy);
          return;
        }
      }

      string snNorm = "", snWhy = "";
      if (!string.IsNullOrWhiteSpace(snIn))
      {
        // Usa TryMake per uniformare la forma snip:*
        if (!SnipHelpers.TryMake(snIn, descr ?? "", 1, out snNorm, out snWhy))
        {
          Emit(DA, outId, outHash, "Invalid Snip: " + snWhy);
          return;
        }
      }

      // Ottieni repository
      object repoObj; string hubInfo;
      ServiceHub.TryGetMetadataRepository(out repoObj, out hubInfo);

      try
      {
        if (isDelete)
        {
          string delInfo; bool ok = MetadataRepositoryCompatExtensions.TryDelete(repoObj, inId, out delInfo);
          outId = inId > 0 ? inId : 0;
          outInfo = string.IsNullOrWhiteSpace(delInfo) ? (ok ? "OK" : "Operazione non riuscita") : delInfo;
          Emit(DA, outId, outHash, outInfo);
          return;
        }

        var payload = new InPayload
        {
          id = inId,
          by = by ?? "",
          info = descr ?? "",
          rf = rfNorm ?? "",
          sn = snNorm ?? ""
        };

        object saved; string upInfo;
        bool upOk = MetadataRepositoryCompatExtensions.TryUpsert(repoObj, payload, out saved, out upInfo);

        if (saved != null) { ReadIf(saved, "Id", ref outId); ReadIf(saved, "Hash", ref outHash); }
        else { outId = inId > 0 ? inId : 0; outHash = ""; }

        outInfo = string.IsNullOrWhiteSpace(upInfo) ? (upOk ? "OK" : "Operazione non riuscita") : upInfo;
        Emit(DA, outId, outHash, outInfo);
      }
      catch (Exception ex) { Emit(DA, outId, outHash, "Errore: " + ex.Message); }
    }

    // helpers
    private sealed class InPayload { public int id { get; set; } public string by { get; set; } public string info { get; set; } public string rf { get; set; } public string sn { get; set; } }
    private static void ReadIf(object obj, string prop, ref int target) { if (obj == null) return; var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase); if (pi == null) return; var v = pi.GetValue(obj, null); int n; if (v is int i) target = i; else if (v != null && int.TryParse(v.ToString(), out n)) target = n; }
    private static void ReadIf(object obj, string prop, ref string target) { if (obj == null) return; var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase); if (pi == null) return; var v = pi.GetValue(obj, null); var s = (v == null ? "" : v.ToString() ?? "").Trim(); if (s.Length > 0) target = s; }
    private static void Emit(IGH_DataAccess DA, int id, string hash, string info) { DA.SetData(0, id); DA.SetData(1, hash ?? ""); DA.SetData(2, info ?? ""); }
  }
}
