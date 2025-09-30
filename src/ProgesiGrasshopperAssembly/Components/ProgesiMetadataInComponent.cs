// ProgesiMetadataInComponent.cs
#nullable disable
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using System;

namespace ProgesiGrasshopperAssembly.Components
{
  public class ProgesiMetadataInComponent : GH_Component
  {
    // Messaggi standardizzati (micro-step 1.2)
    static class Msg
    {
      public const string Idle = "Idle";
      public const string Ok = "OK";
      public const string NotFound = "Non trovato";
      public const string NoRepo = "OK (nessun repo collegato)";
      public static string Invalid(string what) => $"Input non valido: {what}";
      public static string Fail(string what = null) => string.IsNullOrWhiteSpace(what) ? "Operazione non riuscita" : what;
    }

    public ProgesiMetadataInComponent()
        : base("ProgesiMetadataIn", "MetIn",
               "Crea/Aggiorna/Elimina metadata (mock: solo echo).",
               "Progesi", "Metadata")
    { }

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      // Run = FALSE di default (come da specifica)
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);
      p.AddTextParameter("Act", "Act", "Create | Update | Delete", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Id (Update/Delete).", GH_ParamAccess.item, 0);
      p.AddTextParameter("By", "By", "Autore", GH_ParamAccess.item, "");
      p.AddTextParameter("Info", "Info", "Descrizione", GH_ParamAccess.item, "");
      p.AddTextParameter("Ref", "Ref", "Riferimento (url/path)", GH_ParamAccess.item, "");
      p.AddTextParameter("Snip", "Snip", "Snip (base64/url)", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hash risultante.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Messaggio esito.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false; string act = "Create";
      int id = 0; string by = ""; string info = ""; string rf = ""; string sn = "";
      da.GetData(0, ref run);
      da.GetData(1, ref act);
      da.GetData(2, ref id);
      da.GetData(3, ref by);
      da.GetData(4, ref info);
      da.GetData(5, ref rf);
      da.GetData(6, ref sn);

      int oId = 0; string oHash = ""; string oInfo;

      if (!run)
      {
        oInfo = Msg.Idle;
        da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
        return;
      }

      // Convalida minima dell'azione
      bool isCreate = string.Equals(act, "Create", StringComparison.OrdinalIgnoreCase);
      bool isUpdate = string.Equals(act, "Update", StringComparison.OrdinalIgnoreCase);
      bool isDelete = string.Equals(act, "Delete", StringComparison.OrdinalIgnoreCase);

      if (!(isCreate || isUpdate || isDelete))
      {
        oInfo = Msg.Invalid("Act (valori ammessi: Create | Update | Delete)");
        da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
        return;
      }

      if ((isUpdate || isDelete) && id <= 0)
      {
        oInfo = Msg.Invalid("Id (> 0 richiesto per Update/Delete)");
        da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
        return;
      }

      object repo; string repoInfo;
      ServiceHub.TryGetMetadataRepository(out repo, out repoInfo);

      // === Delete ============================================================
      if (isDelete)
      {
        if (repo == null)
        {
          // Nessun repository collegato → manteniamo il comportamento esistente
          oId = id > 0 ? id : 0;
          oInfo = string.IsNullOrWhiteSpace(repoInfo) ? Msg.NoRepo : repoInfo;
          da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
          return;
        }

        string inf;
        // Overload compat: (repo, id, out info)
        bool ok = MetadataRepositoryCompatExtensions.TryDelete(repo, id, out inf);

        oId = id > 0 ? id : 0;
        oInfo = ok ? Msg.Ok : Msg.Fail(inf);

        da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
        return;
      }
      // ======================================================================

      // Create/Update → mock-friendly (echo)
      object persisted; string inf2;
      MetadataRepositoryCompatExtensions.TryUpsert(
        repo,
        new { id = id, by = by, info = info, rf = rf, sn = sn },
        out persisted,
        out inf2);

      // Con esito mock di default: Id=1 su Create / Id passato su Update
      oId = id > 0 ? id : 1;
      oHash = ""; // mock non calcola hash
      oInfo = (repo == null && !string.IsNullOrWhiteSpace(repoInfo)) ? repoInfo :
              (string.IsNullOrWhiteSpace(inf2) ? Msg.Ok : inf2);

      da.SetData(0, oId); da.SetData(1, oHash); da.SetData(2, oInfo);
    }

    public override Guid ComponentGuid => new Guid("9B6AA5E3-6C3B-4C0E-B1B3-86E0B7F1F8C7");
  }
}
