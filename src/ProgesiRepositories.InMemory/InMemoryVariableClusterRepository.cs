using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProgesiCore;

namespace ProgesiRepositories.InMemory
{
  /// <summary>
  /// Repository in-memory per ProgesiVariableCluster.
  /// Implementa IProgesiVariableClusterRepository usando un semplice dizionario.
  /// Utile per test e per scenari dove non vogliamo toccare DB/Rhino.
  /// </summary>
  public sealed class InMemoryVariableClusterRepository : IProgesiVariableClusterRepository
  {
    private readonly ConcurrentDictionary<int, ProgesiVariableCluster> _store =
      new ConcurrentDictionary<int, ProgesiVariableCluster>();

    /// <summary>
    /// Salva (o aggiorna) un cluster in memoria.
    /// </summary>
    public Task<ProgesiVariableCluster> SaveAsync(
      ProgesiVariableCluster cluster,
      CancellationToken ct = default)
    {
      if (cluster is null) throw new ArgumentNullException(nameof(cluster));

      _store[cluster.Id] = cluster;
      return Task.FromResult(cluster);
    }

    /// <summary>
    /// Restituisce il cluster con l'Id specificato, oppure null se non esiste.
    /// </summary>
    public Task<ProgesiVariableCluster?> GetByIdAsync(
      int id,
      CancellationToken ct = default)
    {
      _store.TryGetValue(id, out var cluster);
      return Task.FromResult<ProgesiVariableCluster?>(cluster);
    }

    /// <summary>
    /// Restituisce il cluster che ha l'hashtag indicato, oppure null se non trovato.
    /// </summary>
    public Task<ProgesiVariableCluster?> GetByHashtagAsync(
      string hashtag,
      CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return Task.FromResult<ProgesiVariableCluster?>(null);

      var match = _store.Values
        .FirstOrDefault(c => string.Equals(c.Hashtag, hashtag, StringComparison.Ordinal));

      return Task.FromResult<ProgesiVariableCluster?>(match);
    }

    /// <summary>
    /// Restituisce tutti i cluster presenti nello store in-memory.
    /// </summary>
    public Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(
      CancellationToken ct = default)
    {
      IReadOnlyList<ProgesiVariableCluster> list = _store.Values.ToList();
      return Task.FromResult(list);
    }

    /// <summary>
    /// Elimina il cluster con Id specificato.
    /// </summary>
    public Task<bool> DeleteAsync(
      int id,
      CancellationToken ct = default)
    {
      var removed = _store.TryRemove(id, out _);
      return Task.FromResult(removed);
    }

    /// <summary>
    /// Elimina in blocco i cluster con gli Id specificati.
    /// Ritorna il numero effettivo di elementi rimossi.
    /// </summary>
    public Task<int> DeleteManyAsync(
      IEnumerable<int> ids,
      CancellationToken ct = default)
    {
      if (ids is null) throw new ArgumentNullException(nameof(ids));

      int count = 0;
      foreach (var id in ids)
      {
        if (_store.TryRemove(id, out _))
          count++;
      }

      return Task.FromResult(count);
    }
  }
}
