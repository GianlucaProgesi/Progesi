using System;
using System.Collections.Concurrent;
using System.Linq;
using Progesi.DomainServices.Interfaces;
using Progesi.DomainServices.Models;

namespace Progesi.DomainServices.Services
{
  public class InMemoryVariableService : IProgesiVariableService
  {
    private readonly ConcurrentDictionary<Guid, ProgesiVariable> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _nameIndex =
        new(StringComparer.OrdinalIgnoreCase);

    public ProgesiVariable CreateOrUpdate(ProgesiVariable input)
    {
      if (string.IsNullOrWhiteSpace(input.Name))
        throw new ArgumentException("Name is required", nameof(input));

      // enforce unique name
      if (_nameIndex.TryGetValue(input.Name, out var existingId)
          && existingId != input.Id)
      {
        // merge on existing ID
        input.Id = existingId;
      }

      _byId.AddOrUpdate(input.Id, input, (_, __) => input);
      _nameIndex.AddOrUpdate(input.Name, input.Id, (_, __) => input.Id);
      return input;
    }

    public bool Delete(Guid id)
    {
      if (_byId.TryRemove(id, out var removed))
      {
        if (!string.IsNullOrWhiteSpace(removed.Name))
          _nameIndex.TryRemove(removed.Name, out _);
        return true;
      }
      return false;
    }

    public ProgesiVariable? GetById(Guid id) =>
        _byId.TryGetValue(id, out var v) ? v : null;

    public ProgesiVariable? GetByName(string name)
    {
      if (_nameIndex.TryGetValue(name, out var id))
        return GetById(id);
      return null;
    }
  }
}
