// ProgesiClusterOutputComponent.cs
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiCore.Services;
using ProgesiRepositories.Rhino;
using ProgesiCore;


namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Legge un ProgesiVariableCluster da Id o Hash e restituisce:
  /// - Info (fisso, output #0)
  /// - Ids (fisso, output #1)
  /// - Names (fisso, output #2)
  /// - Values (fisso, output #3, GenericObject list)
  /// - Output dinamici (#4..): uno per variabile, GenericObject, nickname = ProgesiVariable.Name (sanitizzato)
  /// </summary>
  public sealed class ProgesiClusterOutputComponent : GH_Component, IGH_VariableParameterComponent
  {
    private bool _pendingDynamicRebuild = false;

    private string _lastSignature = "";

    public ProgesiClusterOutputComponent()
      : base("Progesi Cluster Output", "ClusterOut",
             "Legge un ProgesiVariableCluster da Id o Hash e produce:\n" +
             "- output fissi (Info/Ids/Names/Values)\n" +
             "- output dinamici: un Value per ogni ProgesiVariable nel cluster",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("C94FD91C-120C-44B9-9FFC-C280D1D712B7");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default FALSE).", GH_ParamAccess.item, false);

      p.AddIntegerParameter("Id", "Id",
                            "Id del cluster (opzionale se Hash è valorizzato).",
                            GH_ParamAccess.item, 0);
      Params.Input[Params.Input.Count - 1].Optional = true;

      p.AddTextParameter("Hash", "Hash",
                         "Hashtag del cluster. Se valorizzato, ha precedenza su Id.",
                         GH_ParamAccess.item, string.Empty);
      Params.Input[Params.Input.Count - 1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);

      p.AddIntegerParameter("Ids", "Ids", "Id delle ProgesiVariable nel cluster.", GH_ParamAccess.list);
      p.AddTextParameter("Names", "Names", "Name delle ProgesiVariable nel cluster.", GH_ParamAccess.list);

      // Values list generic
      var values = new Param_GenericObject
      {
        Name = "Values",
        NickName = "Values",
        Description = "Lista dei Value delle ProgesiVariable (GenericObject).",
        Access = GH_ParamAccess.list
      };
      p.AddParameter(values);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false;
      DA.GetData(0, ref run);

      int id = 0;
      DA.GetData(1, ref id);

      string hash = string.Empty;
      DA.GetData(2, ref hash);

      if (!run)
      {
        DA.SetData(0, "Idle");
        DA.SetDataList(1, new int[0]);
        DA.SetDataList(2, new string[0]);
        DA.SetDataList(3, new object[0]);
        // dinamici -> vuoti
        ClearDynamicOutputs(DA);
        return;
      }

      if (string.IsNullOrWhiteSpace(hash) && id <= 0)
      {
        DA.SetData(0, "Input non valido: specificare almeno Id o Hash.");
        DA.SetDataList(1, new int[0]);
        DA.SetDataList(2, new string[0]);
        DA.SetDataList(3, new object[0]);
        ClearDynamicOutputs(DA);
        return;
      }

      object ctx;
      string hub;
      ServiceHub.TryGetMetadataRepository(out ctx, out hub);

      if (!(ctx is ServiceHub.RhinoContext rh))
      {
        DA.SetData(0, "Repo non disponibile (serve documento Rhino attivo). " + (hub ?? ""));
        DA.SetDataList(1, new int[0]);
        DA.SetDataList(2, new string[0]);
        DA.SetDataList(3, new object[0]);
        ClearDynamicOutputs(DA);
        return;
      }

      var ids = new List<int>();
      var names = new List<string>();
      var values = new List<object>();

      string info;

      try
      {
        var clusterRepo = new RhinoVariableClusterRepository(rh.Doc);
        var service = new ClusterService(clusterRepo);

        ProgesiVariableCluster cluster = null;

        if (!string.IsNullOrWhiteSpace(hash))
          cluster = service.GetByHashtagAsync(hash, default).GetAwaiter().GetResult();
        else
          cluster = service.GetByIdAsync(id, default).GetAwaiter().GetResult();

        if (cluster == null)
        {
          DA.SetData(0, "Cluster non trovato.");
          DA.SetDataList(1, new int[0]);
          DA.SetDataList(2, new string[0]);
          DA.SetDataList(3, new object[0]);
          ClearDynamicOutputs(DA);
          return;
        }

        // Guard: cluster vuoto (file importati male / vecchi dati)
        if (cluster.ProgesiVariableIds == null || cluster.ProgesiVariableIds.Count == 0)
        {
          DA.SetData(0, $"Cluster trovato (Id={cluster.Id}) ma non contiene variabili (VariableIds vuoto).");
          DA.SetDataList(1, new int[0]);
          DA.SetDataList(2, new string[0]);
          DA.SetDataList(3, new object[0]);
          ClearDynamicOutputs(DA);
          return;
        }

        var varRepo = new RhinoVariableRepository(rh.Doc);

        int missing = 0;

        // 1) carico in mappa per evitare duplicati e per ricostruire ordine stabile
        var byId = new Dictionary<int, ProgesiCore.ProgesiVariable>();

        foreach (int vid in cluster.ProgesiVariableIds)
        {
          var v = varRepo.GetByIdAsync(vid, default).GetAwaiter().GetResult();
          if (v == null)
          {
            missing++;
            continue;
          }

          // ultimo v vince, ma in pratica vid è univoco
          byId[vid] = v;
        }

        // 2) ricostruisco resolved nell'ordine ESATTO del cluster
        var resolved = new List<ProgesiCore.ProgesiVariable>();
        foreach (int vid in cluster.ProgesiVariableIds)
        {
          if (byId.TryGetValue(vid, out var v))
            resolved.Add(v);
        }


        // firma per output dinamici (Id + lista Id + lista names)
        string signature = ClusterOutNaming.BuildSignature(cluster.Id, resolved);
        bool changed = EnsureDynamicOutputs(signature, resolved);

        // Se ho cambiato il numero di output, esco SUBITO.
        // ApplyDynamicNicknames verrà eseguito al solve successivo.
        if (changed)
          return;

        if (_pendingDynamicRebuild)
        {
          ApplyDynamicNicknames(resolved);
          _pendingDynamicRebuild = false;
        }


        foreach (var v in resolved)
        {
          ids.Add(v.Id);
          string n = string.IsNullOrWhiteSpace(v.Name) ? $"Var-{v.Id}" : v.Name;
          names.Add(n);
          values.Add(v.Value);
        }

        info = $"OK (Cluster Id={cluster.Id}, Vars={cluster.ProgesiVariableIds.Count}, Found={ids.Count}, Missing={missing})";

        DA.SetData(0, info);
        DA.SetDataList(1, ids);
        DA.SetDataList(2, names);
        DA.SetDataList(3, values);

        // Ora è sicuro scrivere nei dinamici
        for (int i = 0; i < values.Count; i++)
          DA.SetData(4 + i, values[i]);

         // se ci sono più output dinamici del necessario, svuota i restanti
        for (int j = 4 + values.Count; j < Params.Output.Count; j++)
        {
          DA.SetData(j, null);
        }
      }
      catch (Exception ex)
      {
        info = "Errore: " + ex.Message;

        DA.SetData(0, info);
        DA.SetDataList(1, new int[0]);
        DA.SetDataList(2, new string[0]);
        DA.SetDataList(3, new object[0]);
        ClearDynamicOutputs(DA);
      }
    }

    private void ApplyDynamicNicknames(List<ProgesiCore.ProgesiVariable> vars)
    {
      int required = vars.Count;
      int current = Math.Max(0, Params.Output.Count - 4);
      if (current < required) required = current; // safety

      var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < required; i++)
      {
        var v = vars[i];
        string baseName = string.IsNullOrWhiteSpace(v.Name) ? $"Var_{v.Id}" : v.Name;
        string nick = ClusterOutNaming.SanitizeNick(baseName);
        if (string.IsNullOrWhiteSpace(nick))
          nick = $"Var_{v.Id}";
        nick = ClusterOutNaming.MakeUnique(nick, used);

        var param = Params.Output[4 + i];
        param.Name = nick;
        param.NickName = nick;
        param.Description = $"Value (GenericObject) for {baseName} (Id={v.Id}).";
        param.Access = GH_ParamAccess.item;
      }

      Params.OnParametersChanged();
    }

    // ---------------- IGH_VariableParameterComponent ----------------

    public bool CanInsertParameter(GH_ParameterSide side, int index)
      => side == GH_ParameterSide.Output && index >= 4;

    public bool CanRemoveParameter(GH_ParameterSide side, int index)
      => side == GH_ParameterSide.Output && index >= 4;

    public IGH_Param CreateParameter(GH_ParameterSide side, int index)
    {
      // solo output dinamici
      var p = new Param_GenericObject
      {
        Name = "Value",
        NickName = "Value",
        Description = "Value (GenericObject) of a ProgesiVariable in the cluster.",
        Access = GH_ParamAccess.item
      };
      return p;
    }

    public bool DestroyParameter(GH_ParameterSide side, int index) => true;

    public void VariableParameterMaintenance()
    {
      // chiamato da GH quando parametri cambiano
      // manteniamo Access item sugli output dinamici
      for (int i = 4; i < Params.Output.Count; i++)
      {
        Params.Output[i].Access = GH_ParamAccess.item;
      }
    }

    // ---------------- Helpers ----------------
    private bool EnsureDynamicOutputs(string signature, List<ProgesiCore.ProgesiVariable> vars)
    {
      // Se firma uguale, non tocchiamo nulla
      if (string.Equals(signature, _lastSignature, StringComparison.Ordinal))
        return false;

      _lastSignature = signature;

      int required = vars.Count;
      int current = Math.Max(0, Params.Output.Count - 4);

      bool changedCount = false;

      // Rimuovi in eccesso
      while (current > required)
      {
        Params.UnregisterOutputParameter(Params.Output[Params.Output.Count - 1], true);
        current--;
        changedCount = true;
      }

      // Aggiungi mancanti
      while (current < required)
      {
        var p = new Param_GenericObject
        {
          Name = "Value",
          NickName = "Value",
          Description = "Value (GenericObject) of a ProgesiVariable in the cluster.",
          Access = GH_ParamAccess.item
        };
        Params.RegisterOutputParam(p);
        current++;
        changedCount = true;
      }

      // Se ho cambiato il numero di parametri: devo uscire dal solve corrente.
      if (changedCount)
      {
        Params.OnParametersChanged();
        _pendingDynamicRebuild = true; // aggiorna nickname nel solve successivo
        ExpireSolution(true);
        return true;
      }

      // Count uguale: non serve ExpireSolution(true), ma nickname può essere diverso
      _pendingDynamicRebuild = true;
      return false;
    }

    private void ClearDynamicOutputs(IGH_DataAccess DA)
    {
      // svuota gli output dinamici correnti (se presenti)
      for (int i = 4; i < Params.Output.Count; i++)
      {
        DA.SetData(i, null);
      }
    }
  }
}
