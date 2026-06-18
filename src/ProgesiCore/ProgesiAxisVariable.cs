using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore
{
  /// <summary>
  /// Contenitore ad asse per UNA singola serie di ProgesiVariable (stesso Name e stesso ValueTypeKey).
  ///
  /// - Tiene solo gli Id delle variabili (non il valore) per restare leggero e disaccoppiato dai repository.
  /// - Le posizioni sono canoniche e sempre NORMALIZZATE nel dominio [0, 1] (curve re-parameterized).
  /// - Supporta più Id per la stessa stazione (es. multiple variabili alternative / versioni).
  /// </summary>
  public sealed class ProgesiAxisVariable : ValueObject
  {
    public const double DefaultTolerance = 1e-6;

    public int Id { get; private set; }

    /// <summary>Nome asse (etichetta, non geometria). La geometria vive nel layer GH.</summary>
    public string AxisName { get; private set; } = string.Empty;

    /// <summary>
    /// Lunghezza reale dell'asse (opzionale). Serve solo per convertire real &lt;-&gt; normalized.
    /// </summary>
    public double? AxisLength { get; private set; }

    /// <summary>Nome della ProgesiVariable mappata (UNICO per l'intero oggetto).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Chiave di tipo della ProgesiVariable.Value (es. "System.Double").
    /// È una stringa deliberatamente, per evitare riferimenti diretti a Type/assembly in core.
    /// </summary>
    public string ValueTypeKey { get; private set; } = string.Empty;

    /// <summary>
    /// Placeholder per futuri modelli di legge/segmenti (oggi resta per compatibilità roadmap).
    /// </summary>
    public int? RuleId { get; private set; }

    // Single-series map: PositionKey -> set of variable ids
    private readonly SortedDictionary<PositionKey, HashSet<int>> _map
      = new SortedDictionary<PositionKey, HashSet<int>>();

    public ProgesiAxisVariable(
      int id,
      string axisName,
      string name,
      string valueTypeKey,
      double? axisLength = null,
      int? ruleId = null)
    {
      Guard.Against.Negative(id, nameof(id));
      Guard.Against.NullOrWhiteSpace(axisName, nameof(axisName));
      Guard.Against.NullOrWhiteSpace(name, nameof(name));
      Guard.Against.NullOrWhiteSpace(valueTypeKey, nameof(valueTypeKey));
      if (axisLength.HasValue) Guard.Against.NegativeOrZero(axisLength.Value, nameof(axisLength));
      if (ruleId.HasValue) Guard.Against.Negative(ruleId.Value, nameof(ruleId));

      Id = id;
      AxisName = axisName.Trim();
      Name = name.Trim();
      ValueTypeKey = valueTypeKey.Trim();
      AxisLength = axisLength;
      RuleId = ruleId;
    }

    /// <summary>
    /// Signature minimale della ProgesiVariable per validazione in core.
    /// </summary>
    public readonly struct ProgesiVariableSignature
    {
      public int Id { get; }
      public string Name { get; }
      public string ValueTypeKey { get; }

      public ProgesiVariableSignature(int id, string name, string valueTypeKey)
      {
        Guard.Against.Negative(id, nameof(id));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(valueTypeKey, nameof(valueTypeKey));

        Id = id;
        Name = name.Trim();
        ValueTypeKey = valueTypeKey.Trim();
      }
    }

    public IReadOnlyDictionary<double, int[]> GetMap(double tol = DefaultTolerance)
    {
      // tol is only used for consistency with callers; keys already bucketed.
      var result = new Dictionary<double, int[]>();
      foreach (var kv in _map)
      {
        result[kv.Key.Value] = kv.Value.OrderBy(x => x).ToArray();
      }
      return result;
    }

    public IReadOnlyCollection<int> GetAt(double positionNormalized, double tol = DefaultTolerance)
    {
      ValidateNormalizedPosition(positionNormalized);
      var key = new PositionKey(positionNormalized, tol);
      return _map.TryGetValue(key, out var set) ? set.OrderBy(x => x).ToArray() : Array.Empty<int>();
    }

    public IEnumerable<(double positionNormalized, int variableId)> EnumerateAll()
    {
      foreach (var inner in _map)
      {
        var posKey = inner.Key;
        var ids = inner.Value;
        foreach (int id in ids.OrderBy(x => x))
          yield return (posKey.Value, id);
      }
    }

    /// <summary>
    /// Aggiunge una variabile alla stazione (posizione normalizzata). Valida Name e ValueTypeKey.
    /// </summary>
    public void Add(ProgesiVariableSignature signature, double positionNormalized, double tol = DefaultTolerance)
    {
      if (!StringComparer.Ordinal.Equals(signature.Name, Name))
        throw new InvalidOperationException($"Signature.Name '{signature.Name}' does not match axis series Name '{Name}'.");

      if (!StringComparer.Ordinal.Equals(signature.ValueTypeKey, ValueTypeKey))
        throw new InvalidOperationException($"Signature.ValueTypeKey '{signature.ValueTypeKey}' does not match axis series ValueTypeKey '{ValueTypeKey}'.");

      AddUnsafe(positionNormalized, signature.Id, tol);
    }

    /// <summary>
    /// Aggiunge un id senza poter verificare Name/ValueTypeKey. Usare solo in contesti controllati (DTO/repo).
    /// </summary>
    internal void AddUnsafe(double positionNormalized, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(positionNormalized);

      var key = new PositionKey(positionNormalized, tol);
      if (!_map.TryGetValue(key, out var set))
      {
        set = new HashSet<int>();
        _map[key] = set;
      }

      set.Add(variableId);
    }

    public bool Move(double fromPositionNormalized, double toPositionNormalized, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(fromPositionNormalized);
      ValidateNormalizedPosition(toPositionNormalized);

      var fromKey = new PositionKey(fromPositionNormalized, tol);
      if (!_map.TryGetValue(fromKey, out var set) || !set.Remove(variableId))
        return false;

      if (set.Count == 0) _map.Remove(fromKey);

      var toKey = new PositionKey(toPositionNormalized, tol);
      if (!_map.TryGetValue(toKey, out var toSet))
      {
        toSet = new HashSet<int>();
        _map[toKey] = toSet;
      }

      toSet.Add(variableId);
      return true;
    }

    public bool RemoveAt(double positionNormalized, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(positionNormalized);

      var key = new PositionKey(positionNormalized, tol);
      if (!_map.TryGetValue(key, out var set)) return false;

      bool removed = set.Remove(variableId);
      if (removed && set.Count == 0) _map.Remove(key);
      return removed;
    }

    public void ReplaceMap(IEnumerable<(double positionNormalized, IEnumerable<int> ids)> entries, double tol = DefaultTolerance)
    {
      Guard.Against.Null(entries, nameof(entries));

      var newMap = new SortedDictionary<PositionKey, HashSet<int>>();
      foreach (var entry in entries)
      {
        double pos = entry.positionNormalized;
        IEnumerable<int> ids = entry.ids;

        ValidateNormalizedPosition(pos);
        Guard.Against.Null(ids, nameof(entries));

        var key = new PositionKey(pos, tol);
        if (!newMap.TryGetValue(key, out var set))
        {
          set = new HashSet<int>();
          newMap[key] = set;
        }

        foreach (int id in ids)
        {
          Guard.Against.Negative(id, nameof(entries));
          set.Add(id);
        }
      }

      _map.Clear();
      foreach (var kv in newMap)
        _map.Add(kv.Key, kv.Value);
    }

    public void SetRule(int? ruleId)
    {
      if (ruleId.HasValue) Guard.Against.Negative(ruleId.Value, nameof(ruleId));
      RuleId = ruleId;
    }

    public void SetAxisLength(double? axisLength)
    {
      if (axisLength.HasValue) Guard.Against.NegativeOrZero(axisLength.Value, nameof(axisLength));
      AxisLength = axisLength;
    }

    /// <summary>Converte una stazione reale (lunghezza lungo curva) in normalizzata [0,1].</summary>
    public double ToNormalizedFromReal(double realStation)
    {
      if (!AxisLength.HasValue)
        throw new InvalidOperationException("AxisLength is required to convert from real to normalized.");
      if (double.IsNaN(realStation) || double.IsInfinity(realStation))
        throw new ArgumentOutOfRangeException(nameof(realStation), "Station must be a finite number.");
      return realStation / AxisLength.Value;
    }

    /// <summary>Converte una stazione normalizzata [0,1] in reale (lunghezza lungo curva).</summary>
    public double ToRealFromNormalized(double normalizedStation)
    {
      if (!AxisLength.HasValue)
        throw new InvalidOperationException("AxisLength is required to convert from normalized to real.");
      ValidateNormalizedPosition(normalizedStation);
      return normalizedStation * AxisLength.Value;
    }

    private static void ValidateNormalizedPosition(double positionNormalized)
    {
      if (double.IsNaN(positionNormalized) || double.IsInfinity(positionNormalized))
        throw new ArgumentOutOfRangeException(nameof(positionNormalized), "Position must be a finite number.");

      if (positionNormalized < -DefaultTolerance || positionNormalized > 1.0 + DefaultTolerance)
        throw new ArgumentOutOfRangeException(nameof(positionNormalized),
          "Position " + positionNormalized + " is outside [0, 1] (± tol). Positions are stored as normalized stations.");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Id;
      yield return AxisName;
      yield return AxisLength.HasValue ? AxisLength.Value : double.NaN;
      yield return Name;
      yield return ValueTypeKey;
      yield return RuleId.HasValue ? RuleId.Value : int.MinValue;

      foreach (var kv in _map)
      {
        yield return kv.Key.Value;
        foreach (int vid in kv.Value.OrderBy(x => x))
          yield return vid;
      }
    }

    private readonly struct PositionKey : IComparable<PositionKey>, IEquatable<PositionKey>
    {
      public double Value { get; }
      private readonly long _bucket;

      public PositionKey(double value, double tol)
      {
        Value = value;
        double t = (tol > 0 ? tol : DefaultTolerance);
        _bucket = (long)Math.Round(value / t);
      }

      public int CompareTo(PositionKey other) { return _bucket.CompareTo(other._bucket); }
      public bool Equals(PositionKey other) { return _bucket == other._bucket; }
      public override bool Equals(object obj) { return obj is PositionKey pk && Equals(pk); }
      public override int GetHashCode() { return _bucket.GetHashCode(); }
      public override string ToString() { return Value.ToString(); }
    }
  }
}
