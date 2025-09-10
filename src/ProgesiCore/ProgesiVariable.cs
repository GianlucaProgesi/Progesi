using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore
{
  public sealed class ProgesiVariable : ValueObject
  {
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public object? Value { get; private set; }   // può essere null
    public int[] DependsFrom { get; private set; } = Array.Empty<int>();
    public int? MetadataId { get; private set; }

    public ProgesiVariable(int id, string name, object? value, IEnumerable<int>? dependsFrom = null, int? metadataId = null)
    {
      _ = Guard.Against.NegativeOrZero(id, nameof(id));
      _ = Guard.Against.NullOrWhiteSpace(name, nameof(name));

      Id = id;
      Name = name;
      Value = value; // null ammesso
      DependsFrom = dependsFrom?.ToArray() ?? Array.Empty<int>();
      MetadataId = metadataId;
    }

    // Nota: il base molto probabilmente richiede IEnumerable<object> NON nullable
    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Id;
      yield return Name;
      // evitare null: convertiamo in string canonicale
      yield return Value is null ? "<null>" : Value.GetType().FullName!;
      yield return ProgesiHash.CanonicalValue(Value);
      foreach (int d in DependsFrom.OrderBy(x => x))
      {
        yield return d;
      }

      yield return MetadataId.HasValue ? (object)MetadataId.Value : "<null>";
    }
  }
}
