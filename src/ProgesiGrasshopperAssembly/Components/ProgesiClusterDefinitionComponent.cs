// ProgesiClusterDefinitionComponent.cs
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using ProgesiCore.Services;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiRepositories.Rhino;

namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Definisce (o recupera) un ProgesiVariableCluster a partire da:
  /// - Id delle ProgesiVariable in input (tree/lista)
  /// - Nome del cluster
  /// - Descrizione opzionale
  ///
  /// Se l'input Ids è vuoto, usa TUTTE le ProgesiVariable presenti nel repository Rhino.
  /// Usa ClusterService + RhinoVariableClusterRepository.
  /// Nessuna UI custom: componente standard GH con input/output visibili.
  /// </summary>
  public sealed class ProgesiClusterDefinitionComponent : GH_Component
  {
    public ProgesiClusterDefinitionComponent()
      : base("Progesi Cluster Definition", "ClusterDef",
             "Crea o recupera un ProgesiVariableCluster (dedup su Name/Description/Ids).\n" +
             "Se l'input Ids è vuoto, vengono usate tutte le ProgesiVariable presenti nel documento Rhino.",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("28C3FBBF-2276-469F-8575-E43CE3E1B216");

    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);

      // Act: 0=Create, 1=Update, 2=Delete
      p.AddIntegerParameter("Act", "Act",
                            "Azione: 0=Create, 1=Update, 2=Delete. Default 0.",
                            GH_ParamAccess.item, 0);

      // Identificazione cluster per Update/Delete (Hash ha precedenza su Id)
      p.AddIntegerParameter("ClusterId", "CId",
                            "Id del cluster (opzionale; usato per Update/Delete se Hash è vuoto).",
                            GH_ParamAccess.item, 0);
      Params.Input[Params.Input.Count - 1].Optional = true;

      p.AddTextParameter("ClusterHash", "CHash",
                         "Hashtag del cluster (opzionale; ha precedenza su Id per Update/Delete).",
                         GH_ParamAccess.item, string.Empty);
      Params.Input[Params.Input.Count - 1].Optional = true;

      // Id delle ProgesiVariable (lista/albero).
      // Se vuoto, verranno usate tutte le variabili presenti nel documento Rhino.
      p.AddIntegerParameter("VarIds", "Ids",
                            "Id delle ProgesiVariable da includere nel cluster (lista/albero). " +
                            "Se vuoto, vengono usate tutte le variabili presenti nel documento Rhino.",
                            GH_ParamAccess.tree);
      Params.Input[Params.Input.Count - 1].Optional = true;

      p.AddTextParameter("Name", "Name", "Nome del cluster.", GH_ParamAccess.item, "ClusterName");

      p.AddTextParameter("Description", "Desc", "Descrizione opzionale del cluster.",
                         GH_ParamAccess.item, string.Empty);
      Params.Input[Params.Input.Count - 1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Id del cluster.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "Hash", "Hashtag del cluster (Id|Name|Ids).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
    }
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false;
      DA.GetData(0, ref run);

      int act = 0;
      DA.GetData(1, ref act);

      int clusterIdIn = 0;
      DA.GetData(2, ref clusterIdIn);

      string clusterHashIn = string.Empty;
      DA.GetData(3, ref clusterHashIn);

      var idsTree = new GH_Structure<GH_Integer>();
      DA.GetDataTree(4, out idsTree);

      string name = "ClusterName";
      DA.GetData(5, ref name);

      string desc = string.Empty;
      DA.GetData(6, ref desc);

      int outId = 0;
      string outHash = string.Empty;
      string outInfo;

      if (!run)
      {
        DA.SetData(0, outId);
        DA.SetData(1, outHash);
        DA.SetData(2, "Idle (imposta Run=true).");
        return;
      }

      // Recupera contesto Rhino
      object ctx;
      string hub;
      ServiceHub.TryGetMetadataRepository(out ctx, out hub);

      if (!(ctx is ServiceHub.RhinoContext rh))
      {
        outInfo = "Repo non disponibile (serve documento Rhino attivo). " + (hub ?? "");
        DA.SetData(0, outId);
        DA.SetData(1, outHash);
        DA.SetData(2, outInfo);
        return;
      }

      var varRepo = new RhinoVariableRepository(rh.Doc);
      var clusterRepo = new RhinoVariableClusterRepository(rh.Doc);

      // Helper: raccoglie Ids dall'input tree
      List<int> ReadIdsFromInput()
      {
        var ids = new List<int>();
        foreach (var goo in idsTree.AllData(true))
        {
          if (goo is GH_Integer ghi)
          {
            int v = ghi.Value;
            if (v > 0) ids.Add(v);
          }
        }
        return ids.Distinct().OrderBy(x => x).ToList();
      }

      // Helper: carica tutte le variabili dal repository Rhino e ritorna lista Id
      List<int> LoadAllVariableIds()
      {
        var ids = new List<int>();
        var allVars = varRepo.GetAllAsync(default).GetAwaiter().GetResult();
        foreach (var v in allVars)
        {
          if (v == null || v.Id <= 0) continue;
          ids.Add(v.Id);
        }
        return ids.Distinct().OrderBy(x => x).ToList();
      }

      // Helper: risolve cluster esistente (Hash ha precedenza)
      ProgesiVariableCluster ResolveExistingCluster()
      {
        ProgesiVariableCluster cluster = null;

        if (!string.IsNullOrWhiteSpace(clusterHashIn))
        {
          cluster = clusterRepo.GetByHashtagAsync(clusterHashIn, default).GetAwaiter().GetResult();
          return cluster;
        }

        if (clusterIdIn > 0)
        {
          cluster = clusterRepo.GetByIdAsync(clusterIdIn, default).GetAwaiter().GetResult();
          return cluster;
        }

        return null;
      }

      try
      {
        // 0=Create, 1=Update, 2=Delete
        if (act == 2)
        {
          // DELETE
          var existing = ResolveExistingCluster();
          if (existing == null)
          {
            outInfo = "Delete: cluster non trovato (fornire Hash o Id valido).";
            DA.SetData(0, 0);
            DA.SetData(1, "");
            DA.SetData(2, outInfo);
            return;
          }

          bool deleted = clusterRepo.DeleteAsync(existing.Id, default).GetAwaiter().GetResult();
          outInfo = deleted
            ? $"OK (Deleted Cluster Id={existing.Id})"
            : $"Delete fallito (Cluster Id={existing.Id})";

          DA.SetData(0, 0);
          DA.SetData(1, "");
          DA.SetData(2, outInfo);
          return;
        }

        // Per Create/Update calcoliamo la lista Ids (se vuota → tutte)
        var ids = ReadIdsFromInput();
        if (ids.Count == 0)
          ids = LoadAllVariableIds();

        if (ids.Count == 0)
        {
          outInfo = "Nessuna variabile disponibile (input Ids vuoto e repository vuoto, oppure variabili inesistenti).";
          DA.SetData(0, 0);
          DA.SetData(1, "");
          DA.SetData(2, outInfo);
          return;
        }

        if (act == 1)
        {
          // UPDATE
          var existing = ResolveExistingCluster();
          if (existing == null)
          {
            outInfo = "Update: cluster non trovato (fornire Hash o Id valido).";
            DA.SetData(0, 0);
            DA.SetData(1, "");
            DA.SetData(2, outInfo);
            return;
          }

          // Manteniamo lo stesso Id: UPDATE vero (non dedup tramite service!)
          var updated = ProgesiVariableCluster.Rehydrate(
            existing.Id,
            name,
            ids,
            desc,
            null);

          var saved = clusterRepo.SaveAsync(updated, default).GetAwaiter().GetResult();

          outId = saved.Id;
          outHash = saved.Hashtag ?? string.Empty;
          outInfo = $"OK (Updated Cluster Id={saved.Id}, Vars={saved.ProgesiVariableIds.Count})";

          DA.SetData(0, outId);
          DA.SetData(1, outHash);
          DA.SetData(2, outInfo);
          return;
        }

        // CREATE (default)
        {
          var service = new ClusterService(clusterRepo);
          var cluster = service.CreateOrGetClusterAsync(name, ids, desc).GetAwaiter().GetResult();

          outId = cluster.Id;
          outHash = cluster.Hashtag ?? string.Empty;
          outInfo = $"OK (Cluster Id={cluster.Id}, Vars={cluster.ProgesiVariableIds.Count})";

          DA.SetData(0, outId);
          DA.SetData(1, outHash);
          DA.SetData(2, outInfo);
          return;
        }
      }
      catch (Exception ex)
      {
        outInfo = "Errore: " + ex.Message;
        DA.SetData(0, 0);
        DA.SetData(1, "");
        DA.SetData(2, outInfo);
        return;
      }
    }


  }
}
