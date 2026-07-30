using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace ProgesiCore.Services
{
  /// <summary>
  /// Implementazione base di IClusterService.
  /// Usa solo l'interfaccia IProgesiVariableClusterRepository,
  /// quindi funziona con InMemory, SQLite, Rhino, EF, ecc.
  /// </summary>
  public sealed class ClusterService : IClusterService
  {
    private readonly IProgesiVariableClusterRepository _clusterRepository;
    private readonly IVariableRepository? _variableRepository;

    public ClusterService(
      IProgesiVariableClusterRepository clusterRepository,
      IVariableRepository? variableRepository = null)
    {
      _clusterRepository = clusterRepository ?? throw new ArgumentNullException(nameof(clusterRepository));
      _variableRepository = variableRepository;
    }

    public async Task<ProgesiVariableCluster> CreateOrGetClusterAsync(
      string name,
      IEnumerable<int> progesiVariableIds,
      string? description = null,
      CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Cluster name is required.", nameof(name));

      if (progesiVariableIds is null)
        throw new ArgumentNullException(nameof(progesiVariableIds));

      // normalizza Id: >0, distinct, ordinati
      var ids = progesiVariableIds
        .Where(id => id > 0)
        .Distinct()
        .OrderBy(id => id)
        .ToList();

      if (ids.Count == 0)
        throw new ArgumentException("Cluster must contain at least one variable id.", nameof(progesiVariableIds));

      // R2-G: referential integrity — reject a cluster that references non-existent ProgesiVariable(s).
      // Only enforced when a variable repository is supplied (create path); read paths pass null.
      if (_variableRepository != null)
      {
        var missing = new List<int>();
        foreach (var vid in ids)
        {
          var existingVar = await _variableRepository.GetByIdAsync(vid, ct).ConfigureAwait(false);
          if (existingVar == null) missing.Add(vid);
        }
        if (missing.Count > 0)
          throw new ArgumentException(
            $"Cluster references non-existent ProgesiVariable id(s): {string.Join(", ", missing)}.",
            nameof(progesiVariableIds));
      }

      // Candidate "logico" (Id=0) solo per confronto con i cluster esistenti
      var candidate = ProgesiVariableCluster.CreateNew(name, ids, description);

      // Recupera tutti i cluster esistenti dallo store
      var existing = await _clusterRepository.GetAllAsync(ct).ConfigureAwait(false);

      // Dedup logico: stesso Name, Description e lista di Id (ordinata) → cluster equivalente
      var match = existing.FirstOrDefault(c => c.IsEquivalentTo(candidate));
      if (match != null)
      {
        return match;
      }

      // Nuovo Id = max(Id) + 1 (o 1 se non ci sono cluster)
      var nextId = existing.Count == 0 ? 1 : existing.Max(c => c.Id) + 1;

      // Creiamo il cluster reale con Id assegnato;
      // Rehydrate calcola anche l'Hashtag coerente.
      var newCluster = ProgesiVariableCluster.Rehydrate(nextId, name, ids, description, null);

      // Salviamo sul repository (che può avere ulteriore dedup via ContentHash, come in SQLite)
      var saved = await _clusterRepository.SaveAsync(newCluster, ct).ConfigureAwait(false);
      return saved;
    }

    public Task<ProgesiVariableCluster?> GetByIdAsync(
      int id,
      CancellationToken ct = default)
      => _clusterRepository.GetByIdAsync(id, ct);

    public Task<ProgesiVariableCluster?> GetByHashtagAsync(
      string hashtag,
      CancellationToken ct = default)
      => _clusterRepository.GetByHashtagAsync(hashtag, ct);

    public Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(
      CancellationToken ct = default)
      => _clusterRepository.GetAllAsync(ct);

    public Task<bool> DeleteAsync(
      int id,
      CancellationToken ct = default)
      => _clusterRepository.DeleteAsync(id, ct);

    public Task<int> DeleteManyAsync(
      IEnumerable<int> ids,
      CancellationToken ct = default)
      => _clusterRepository.DeleteManyAsync(ids, ct);

    // R2-G: when a ProgesiVariable is deleted, strip it from every cluster that references it.
    // A cluster left with no variables is deleted (cluster invariant is >= 1 variable).
    public async Task<CascadeResult> CascadeRemoveVariableFromClustersAsync(
      int variableId,
      CancellationToken ct = default)
    {
      if (variableId <= 0) return new CascadeResult(0, Array.Empty<int>());

      var all = await _clusterRepository.GetAllAsync(ct).ConfigureAwait(false);
      int applied = 0;
      var failedClusterIds = new List<int>();

      foreach (var cluster in all)
      {
        if (cluster.ProgesiVariableIds == null || !cluster.ProgesiVariableIds.Contains(variableId))
          continue;

        try
        {
          var remaining = cluster.ProgesiVariableIds.Where(v => v != variableId).ToList();

          if (remaining.Count == 0)
          {
            await _clusterRepository.DeleteAsync(cluster.Id, ct).ConfigureAwait(false);
          }
          else
          {
            var updated = ProgesiVariableCluster.Rehydrate(
              cluster.Id, cluster.Name, remaining, cluster.Description, null);
            await _clusterRepository.SaveAsync(updated, ct).ConfigureAwait(false);
          }

          applied++;
        }
        catch
        {
          failedClusterIds.Add(cluster.Id);
        }
      }

      return new CascadeResult(applied, failedClusterIds);
    }
  }
}
