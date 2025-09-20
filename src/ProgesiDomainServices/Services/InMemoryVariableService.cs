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

      // Se l'Id è vuoto, prova a risolvere per Name (Name come chiave logica univoca)
      if (v.Id == Guid.Empty)
      {
        var byName = GetByName(v.Name);
        v.Id = byName != null ? byName.Id : Guid.NewGuid();
      }

      // Upsert (copio per evitare mutazioni esterne)
      var copy = new ProgesiVariable
      {
        Id = v.Id,
        Name = v.Name?.Trim(),
        Value = v.Value,
        Unit = v.Unit ?? "",
        Type = v.Type ?? ""
      };

      _store[copy.Id] = copy;
      return copy;
    }

    public ProgesiVariable GetById(Guid id)
    {
      if (id == Guid.Empty) return null;
      _store.TryGetValue(id, out var v);
      return v; // può essere null in net48
    }

    public ProgesiVariable GetByName(string name)
    {
      if (string.IsNullOrWhiteSpace(name)) return null;
      return _store.Values.FirstOrDefault(x =>
          string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool Delete(Guid id) => _store.TryRemove(id, out _);
  }
}
