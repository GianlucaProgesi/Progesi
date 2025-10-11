using System;
using System.Collections.Concurrent;
using System.Linq;
using Progesi.DomainServices.Interfaces;
using Progesi.DomainServices.Models;

namespace Progesi.DomainServices.Services
{
  public class InMemoryVariableService : IProgesiVariableService
  {
    private readonly ConcurrentDictionary<Guid, ProgesiVariable> _store =
        new ConcurrentDictionary<Guid, ProgesiVariable>();

    public ProgesiVariable CreateOrUpdate(ProgesiVariable v)
    {
      if (v == null) throw new ArgumentNullException(nameof(v));
      if (string.IsNullOrWhiteSpace(v.Name))
        throw new ArgumentException("Name is required for ProgesiVariable.", nameof(v));

      // Se Id mancante, prova a risolvere per Name
      if (v.Id == Guid.Empty)
      {
        ProgesiVariable? byName = GetByName(v.Name); // <- nullable locale
        if (byName != null)
          v.Id = byName.Id;
        else
          v.Id = Guid.NewGuid();
      }

      // Upsert (copio per evitare mutazioni esterne)
      var copy = new ProgesiVariable
      {
        Id = v.Id,
        Name = v.Name?.Trim() ?? string.Empty,
        Value = v.Value,
        Unit = v.Unit ?? string.Empty,
        Type = v.Type ?? string.Empty
      };

      _store[copy.Id] = copy;
      return copy;
    }

    public ProgesiVariable GetById(Guid id)
    {
      if (id == Guid.Empty) return null!;                // compat interfaccia non-nullable
      _store.TryGetValue(id, out var v);
      return v!;                                         // può essere null a runtime
    }

    public ProgesiVariable GetByName(string name)
    {
      if (string.IsNullOrWhiteSpace(name)) return null!;
      var match = _store.Values.FirstOrDefault(x =>
          string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
      return match!;                                     // può essere null a runtime
    }

    public bool Delete(Guid id) => _store.TryRemove(id, out _);
  }
}
