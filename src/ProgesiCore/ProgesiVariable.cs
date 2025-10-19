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
    /// <summary>
    /// True se il valore è un'ipotesi (assumption) provvisoria.
    /// Influenza uguaglianza e calcolo dell'hash.
    /// </summary>
    public bool IsAssumption { get; private set; } = false;

    public ProgesiVariable(int id, string name, object? value, IEnumerable<int>? dependsFrom = null, int? metadataId = null, bool isAssumption = false)
    {
      Guard.Against.Negative(id, nameof(id));
      Guard.Against.NullOrWhiteSpace(name, nameof(name));

      Id = id;
      Name = name;
      Value = value; // null ammesso
      DependsFrom = (dependsFrom ?? Array.Empty<int>()).ToArray();
      MetadataId = metadataId;
      IsAssumption = isAssumption;
    }

    public ProgesiVariable WithValue(object? value)
      => new ProgesiVariable(Id, Name, value, DependsFrom, MetadataId, IsAssumption);

    public ProgesiVariable WithDependsFrom(IEnumerable<int>? dependsFrom)
      => new ProgesiVariable(Id, Name, Value, dependsFrom ?? Array.Empty<int>(), MetadataId, IsAssumption);

    public ProgesiVariable WithMetadataId(int? metadataId)
      => new ProgesiVariable(Id, Name, Value, DependsFrom, metadataId, IsAssumption);

    public ProgesiVariable WithIsAssumption(bool isAssumption)
      => new ProgesiVariable(Id, Name, Value, DependsFrom, MetadataId, isAssumption);

    // Nota: il base richiede IEnumerable<object> NON nullable
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
      yield return IsAssumption; // nuovo componente rilevante
    }
  }
}
