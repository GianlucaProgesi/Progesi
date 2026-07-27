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
    public int[] MetadataIds { get; private set; } = Array.Empty<int>();
    /// <summary>First linked metadata id, or null when the list is empty (read-only compat).</summary>
    public int? MetadataId => MetadataIds.Length > 0 ? MetadataIds[0] : (int?)null;
    /// <summary>
    /// True se il valore è un'ipotesi (assumption) provvisoria.
    /// Influenza uguaglianza e calcolo dell'hash.
    /// </summary>
    public bool IsAssumption { get; private set; } = false;

    /// <summary>Content-based hashtag (SHA-256 digest; derived, not part of equality).</summary>
    public string Hashtag => ProgesiHash.Compute(this);

    public ProgesiVariable(int id, string name, object? value, IEnumerable<int>? dependsFrom = null, IEnumerable<int>? metadataIds = null, bool isAssumption = false)
    {
      Guard.Against.Negative(id, nameof(id));
      Guard.Against.NullOrWhiteSpace(name, nameof(name));

      Id = id;
      Name = name;
      Value = value; // null ammesso
      DependsFrom = (dependsFrom ?? Array.Empty<int>()).ToArray();
      MetadataIds = NormalizeMetadataIds(metadataIds);
      IsAssumption = isAssumption;
    }

    public ProgesiVariable WithValue(object? value)
      => new ProgesiVariable(Id, Name, value, DependsFrom, MetadataIds, IsAssumption);

    public ProgesiVariable WithDependsFrom(IEnumerable<int>? dependsFrom)
      => new ProgesiVariable(Id, Name, Value, dependsFrom ?? Array.Empty<int>(), MetadataIds, IsAssumption);

    public ProgesiVariable WithMetadataIds(IEnumerable<int>? metadataIds)
      => new ProgesiVariable(Id, Name, Value, DependsFrom, metadataIds, IsAssumption);

    public ProgesiVariable WithMetadataId(int? metadataId)
      => WithMetadataIds(metadataId.HasValue && metadataId.Value > 0 ? new[] { metadataId.Value } : Array.Empty<int>());

    public ProgesiVariable WithIsAssumption(bool isAssumption)
      => new ProgesiVariable(Id, Name, Value, DependsFrom, MetadataIds, isAssumption);

    private static int[] NormalizeMetadataIds(IEnumerable<int>? metadataIds)
    {
      if (metadataIds == null)
        return Array.Empty<int>();

      var seen = new HashSet<int>();
      var list = new List<int>();
      foreach (var id in metadataIds)
      {
        if (id <= 0 || !seen.Add(id))
          continue;
        list.Add(id);
      }

      return list.ToArray();
    }

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
      foreach (int m in MetadataIds.OrderBy(x => x))
      {
        yield return m;
      }
      yield return IsAssumption; // nuovo componente rilevante
    }
  }
}
