using ProgesiCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Progesi.Core.Variables
{
  // Adatta il namespace / base class ai tuoi progetti
  public sealed class ProgesiVariableCluster : ValueObject
  {
    // Necessario per i repository (Rhino/SQLite/EF)
    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Id delle ProgesiVariable raggruppate dal cluster.
    /// Sempre ordinati e senza duplicati.
    /// </summary>
    public IReadOnlyList<int> ProgesiVariableIds => _progesiVariableIds;

    private readonly List<int> _progesiVariableIds = new List<int>();

    /// <summary>
    /// Descrizione opzionale del cluster.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Hashtag deterministico del cluster.
    /// Basato su: Id + Name + lista ordinata degli Id delle ProgesiVariable.
    /// </summary>
    public string Hashtag { get; private set; }

    // Costruttore privato per EF / serializer
    private ProgesiVariableCluster()
    {
      Name = string.Empty;
      Description = string.Empty;
      Hashtag = string.Empty;
    }

    private ProgesiVariableCluster(
        int id,
        string name,
        IEnumerable<int> progesiVariableIds,
        string? description)
    {
      if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Cluster name cannot be null or empty.", nameof(name));

      var ids = (progesiVariableIds ?? Enumerable.Empty<int>())
          .Where(v => v > 0)
          .Distinct()
          .OrderBy(v => v)
          .ToList();

      if (!ids.Any())
        throw new ArgumentException("Cluster must contain at least one ProgesiVariable id.", nameof(progesiVariableIds));

      if (id < 0)
        throw new ArgumentOutOfRangeException(nameof(id), "Id cannot be negative.");

      Id = id;
      Name = name.Trim();
      Description = description?.Trim() ?? string.Empty;

      _progesiVariableIds.Clear();
      _progesiVariableIds.AddRange(ids);

      Hashtag = BuildHashtag(Id, Name, _progesiVariableIds);
    }

    /// <summary>
    /// Factory principale per creare un nuovo cluster lato dominio.
    /// Id tipicamente 0 prima del salvataggio su repository.
    /// </summary>
    public static ProgesiVariableCluster CreateNew(
        string name,
        IEnumerable<int> progesiVariableIds,
        string? description = null)
    {
      // Id = 0 (non ancora persistito)
      return new ProgesiVariableCluster(0, name, progesiVariableIds, description);
    }

    /// <summary>
    /// Factory pensata per la ricostruzione da repository
    /// quando l'Id è già noto (ad esempio da DB).
    /// </summary>
    public static ProgesiVariableCluster Rehydrate(
        int id,
        string name,
        IEnumerable<int> progesiVariableIds,
        string? description,
        string? hashtagFromStore = null)
    {
      var cluster = new ProgesiVariableCluster(id, name, progesiVariableIds, description);

      // Se lo storage ha già un hashtag, possiamo fidarci,
      // altrimenti lo ricalcoliamo.
      if (!string.IsNullOrWhiteSpace(hashtagFromStore))
      {
        cluster.Hashtag = hashtagFromStore!.Trim();
      }

      return cluster;
    }

    /// <summary>
    /// Versione con Id assegnato, ad esempio dopo un inserimento su DB.
    /// Ritorna una nuova istanza con Id aggiornato e Hashtag ricalcolato.
    /// </summary>
    public ProgesiVariableCluster WithId(int newId)
    {
      if (newId <= 0)
        throw new ArgumentOutOfRangeException(nameof(newId), "Cluster id must be positive.");

      return new ProgesiVariableCluster(
          newId,
          Name,
          _progesiVariableIds,
          Description);
    }

    /// <summary>
    /// Determina se due cluster sono logicamente equivalenti,
    /// indipendentemente dall'Id assegnato dal repository.
    /// </summary>
    public bool IsEquivalentTo(ProgesiVariableCluster? other)
    {
      if (other is null) return false;

      return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
             string.Equals(Description, other.Description, StringComparison.Ordinal) &&
             _progesiVariableIds.SequenceEqual(other._progesiVariableIds);
    }

    /// <summary>
    /// Ricalcola l'hashtag del cluster in base allo stato corrente.
    /// Utile se per qualche motivo hai dovuto cambiare Id o lista di variabili.
    /// </summary>
    public ProgesiVariableCluster RecalculateHashtag()
    {
      Hashtag = BuildHashtag(Id, Name, _progesiVariableIds);
      return this;
    }

    private static string BuildHashtag(int id, string name, IEnumerable<int> orderedIds)
    {
      var idsPart = string.Join(",", orderedIds);
      var seed = $"{id}|{name.Trim()}|{idsPart}";

      // QUI puoi sostituire con la stessa logica usata per Variable/Metadata
      // ad es. passando seed a un servizio SHA-256 comune.
      return seed;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      // L'uguaglianza del ValueObject ignora l'Id,
      // ma considera tutte le altre proprietà significative.
      yield return Name;
      yield return Description;

      foreach (var id in _progesiVariableIds)
        yield return id;
    }

    public override string ToString()
    {
      return $"Cluster(Id={Id}, Name={Name}, Count={_progesiVariableIds.Count})";
    }
  }
}
