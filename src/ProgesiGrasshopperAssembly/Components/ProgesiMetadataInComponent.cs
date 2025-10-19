#nullable disable
using System;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiMetadataInComponent : GH_Component
  {
    public ProgesiMetadataInComponent()
      : base("ProgesiMetadataIn", "MetIn",
             "Create/Update/Delete metadata (Rhino repository).",
             "Progesi", "Metadata")
    { }

    public override Guid ComponentGuid => new Guid("0E9C7A49-3F5C-4F0D-9A4C-0C9E2B9A6B17");
    // FIX: nome icona corretto è MetIn
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.MetIn;

    // IN: Run, Act, Id, By, Description, Ref, Snip
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "Create | Update | Delete", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Id per Update/Delete.", GH_ParamAccess.item, 0);
      p.AddTextParameter("By", "By", "Autore (es. GM).", GH_ParamAccess.item, "");
      // rinominato: Info -> Description (evita collisione con l'output Info)
      p.AddTextParameter("Description", "Descr", "Descrizione / note (usata per dedupe con By).", GH_ParamAccess.item, "");
      p.AddTextParameter("Ref", "Ref", "Riferimenti separati da '|'.", GH_ParamAccess.item, "");
      p.AddTextParameter("Snip", "Snip", "Snip in formato supportato (snip:/data-url/base64/path/url).", GH_ParamAccess.item, "");
    }

    // OUT
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Riepilogo umano: ID/BY/DESC (non include Ref/Snip).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
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

      if (!(isCreate || isUpdate || isDelete)) { Emit(DA, outId, outHash, "Input non valido: Act"); return; }
      if ((isUpdate || isDelete) && inId <= 0) { Emit(DA, outId, outHash, "Input non valido: Id (>0) richiesto per Update/Delete)"); return; }

      // Normalizzazione/validazione Ref & Snip
      string rfNorm = "", refWhy = "";
      if (!string.IsNullOrWhiteSpace(rfIn))
      {
        var refs = rfIn.Split('|');
        var normList = new System.Collections.Generic.List<string>();
        foreach (var r in refs)
        {
          var s = r?.Trim(); if (string.IsNullOrEmpty(s)) continue;
          if (SnipHelpers.TryNormalizeRef(s, out var n, out var why)) normList.Add(n);
          else { refWhy = string.IsNullOrWhiteSpace(refWhy) ? why : (refWhy + "; " + why); }
        }
        rfNorm = string.Join("|", normList);
      }

      string snNorm = "", snWhy = "";
      if (!string.IsNullOrWhiteSpace(snIn))
      {
        if (!SnipHelpers.TryMake(snIn, caption: "", index: 1, out snNorm, out snWhy))
          snNorm = ""; // silenzioso
      }

      object repo; string hub;
      ServiceHub.TryGetMetadataRepository(out repo, out hub);

      try
      {
        if (isDelete)
        {
          string info; bool ok = MetadataRepositoryCompatExtensions.TryDelete(repo, inId, out info);
          outId = inId > 0 ? inId : 0; outHash = ""; outInfo = string.IsNullOrWhiteSpace(info) ? (ok ? "OK" : "Operazione non riuscita") : info;
          Emit(DA, outId, outHash, outInfo); return;
        }

        var payload = new
        {
          id = inId,
          by = by ?? "",
          info = descr ?? "",   // NB: “info” = Description
          rf = rfNorm ?? "",
          sn = snNorm ?? ""
        };

        object saved; string upInfo;
        bool upOk = MetadataRepositoryCompatExtensions.TryUpsert(repo, payload, out saved, out upInfo);

        if (saved != null) { ReadIf(saved, "Id", ref outId); ReadIf(saved, "Hash", ref outHash); }
        else { outId = inId > 0 ? inId : 0; outHash = ""; }

        string summary = ""; ReadIf(saved, "Summary", ref summary);
        var prefix = string.IsNullOrWhiteSpace(upInfo) ? (upOk ? "OK" : "Operazione non riuscita") : upInfo;
        outInfo = string.IsNullOrWhiteSpace(summary) ? prefix : $"{prefix} | {summary}";

        Emit(DA, outId, outHash, outInfo);
      }
      catch (Exception ex) { Emit(DA, outId, outHash, "Errore: " + ex.Message); }
    }

    private static void ReadIf(object obj, string prop, ref int target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      if (v == null) return;
      int n; if (int.TryParse(v.ToString(), out n)) target = n;
    }

    private static void ReadIf(object obj, string prop, ref string target)
    {
      if (obj == null) return;
      var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
      if (pi == null) return;
      var v = pi.GetValue(obj, null);
      target = v == null ? "" : v.ToString();
    }

    private void Emit(IGH_DataAccess DA, int id, string hash, string info)
    {
      DA.SetData(0, id);
      DA.SetData(1, hash ?? "");
      DA.SetData(2, info ?? "");
    }
  }
}
