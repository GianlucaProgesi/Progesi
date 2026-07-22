#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProgesiCore;
using ProgesiRepositories.InMemory;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Hub di servizio per i componenti GH che lavorano con i contratti di ProgesiCore.
  /// </summary>
  internal static class ServiceHubCore
  {
    private static readonly IVariableRepository _variables = new InMemoryVariableRepository();

    /// <summary>Repository per ProgesiVariable (contratto di Core).</summary>
    public static IVariableRepository Variables => _variables;

    /// <summary>
    /// Repository metadati locale e super-difensivo (mai null).
    /// In P1 lo usiamo per non dipendere da storage esterni.
    /// </summary>
    private sealed class LocalMetadataRepository : IMetadataRepository
    {
      // conteniamo in memoria una collezione sempre non-null
      private readonly List<ProgesiMetadata> _items = new List<ProgesiMetadata>();

      public Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip, int take, CancellationToken ct)
      {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > _items.Count) take = _items.Count - skip;
        if (take < 0) take = 0;

        // copia difensiva
        var result = new List<ProgesiMetadata>();
        for (int i = skip; i < skip + take && i < _items.Count; i++)
          result.Add(_items[i]);
        return Task.FromResult((IReadOnlyList<ProgesiMetadata>)result);
      }

      public Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct)
      {
        for (int i = 0; i < _items.Count; i++)
          if (_items[i].Id == id)
            return Task.FromResult<ProgesiMetadata?>(_items[i]);
        return Task.FromResult<ProgesiMetadata?>(null);
      }

      public Task UpsertAsync(ProgesiMetadata m, CancellationToken ct)
      {
        if (m == null) return Task.CompletedTask;

        // cerca e sostituisce
        for (int i = 0; i < _items.Count; i++)
        {
          if (_items[i].Id == m.Id)
          {
            _items[i] = m;
            return Task.CompletedTask;
          }
        }
        _items.Add(m);
        return Task.CompletedTask;
      }

      public Task<bool> DeleteAsync(int id, CancellationToken ct)
      {
        for (int i = 0; i < _items.Count; i++)
        {
          if (_items[i].Id == id)
          {
            _items.RemoveAt(i);
            return Task.FromResult(true);
          }
        }
        return Task.FromResult(false);
      }
    }

    private static readonly IMetadataRepository _metadata = new LocalMetadataRepository();
    public static IMetadataRepository Metadata => _metadata;
  }
}
