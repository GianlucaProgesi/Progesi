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

    /// <summary>Opaque serialized curve geometry (Rhino-free round-trip payload for adapters).</summary>
    public string CurvePayload { get; private set; } = string.Empty;

    /// <summary>Canonical curve interpretation mode.</summary>
    public AxisCurveMode Mode { get; private set; } = AxisCurveMode.Curve3d;

    /// <summary>Normalized [0,1] positions where variables are always defined/interpolated.</summary>
    public IReadOnlyList<double> KeyPoints { get; private set; } = Array.Empty<double>();

    /// <summary>Nome della ProgesiVariable mappata (UNICO per l'intero oggetto).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Chiave di tipo della ProgesiVariable.Value (es. "System.Double").
    /// È una stringa deliberatamente, per evitare riferimenti diretti a Type/assembly in core.
    /// </summary>
    public string ValueTypeKey { get; private set; } = string.Empty;

    /// <summary>
    /// Legacy placeholder retained for back-compat; prefer <see cref="FunctionRef"/>.
    /// </summary>
    public int? RuleId { get; private set; }

    /// <summary>Reference to or embedded copy of the governing ProgesiFunction.</summary>
    public ProgesiFunctionRef FunctionRef { get; private set; } = ProgesiFunctionRef.Empty;

    /// <summary>Content-based hashtag (SHA-256 digest; derived, not part of equality).</summary>
    public string Hashtag => ProgesiHash.Compute(this);

    /// <summary>Alias aligned with Cluster/Metadata naming.</summary>
    public string ContentHash => Hashtag;

    // Single-series map: (PositionKey, Side) -> set of variable ids
    private readonly SortedDictionary<StationKey, HashSet<int>> _map
      = new SortedDictionary<StationKey, HashSet<int>>();

    // Per-station labels (attach to the axis, not the deduped ProgesiVariable)
    private readonly SortedDictionary<PositionKey, string> _labels
      = new SortedDictionary<PositionKey, string>();

    public ProgesiAxisVariable(
      int id,
      string axisName,
      string name,
      string valueTypeKey,
      double? axisLength = null,
      int? ruleId = null,
      string? curvePayload = null,
      AxisCurveMode mode = AxisCurveMode.Curve3d,
      IEnumerable<double>? keyPoints = null,
      ProgesiFunctionRef? functionRef = null)
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
      CurvePayload = curvePayload ?? string.Empty;
      Mode = mode;
      FunctionRef = functionRef ?? ProgesiFunctionRef.Empty;
      KeyPoints = NormalizeKeyPoints(keyPoints);
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
      var result = new Dictionary<double, int[]>();
      foreach (var kv in _map.Where(kv => kv.Key.Side == ProgesiAxisStationSide.None))
        result[kv.Key.Position.Value] = kv.Value.OrderBy(x => x).ToArray();
      return result;
    }

    public IReadOnlyCollection<int> GetAt(
      double positionNormalized,
      ProgesiAxisStationSide side = ProgesiAxisStationSide.None,
      double tol = DefaultTolerance)
    {
      ValidateNormalizedPosition(positionNormalized);
      var key = new StationKey(new PositionKey(positionNormalized, tol), side);
      return _map.TryGetValue(key, out var set) ? set.OrderBy(x => x).ToArray() : Array.Empty<int>();
    }

    public IEnumerable<(double positionNormalized, int variableId, ProgesiAxisStationSide side)> EnumerateAll()
    {
      foreach (var inner in _map)
      {
        var stationKey = inner.Key;
        var ids = inner.Value;
        foreach (int vid in ids.OrderBy(x => x))
          yield return (stationKey.Position.Value, vid, stationKey.Side);
      }
    }

    public IReadOnlyDictionary<double, string> GetLabels(double tol = DefaultTolerance)
    {
      var result = new Dictionary<double, string>();
      foreach (var kv in _labels)
        result[kv.Key.Value] = kv.Value;
      return result;
    }

    public string? GetLabel(double positionNormalized, double tol = DefaultTolerance)
    {
      ValidateNormalizedPosition(positionNormalized);
      var key = new PositionKey(positionNormalized, tol);
      return _labels.TryGetValue(key, out var label) ? label : null;
    }

    public void SetLabel(double positionNormalized, string? label, double tol = DefaultTolerance)
    {
      ValidateNormalizedPosition(positionNormalized);
      var key = new PositionKey(positionNormalized, tol);
      if (string.IsNullOrWhiteSpace(label))
      {
        _labels.Remove(key);
        return;
      }

      _labels[key] = label.Trim();
    }

    public bool RemoveLabel(double positionNormalized, double tol = DefaultTolerance)
    {
      ValidateNormalizedPosition(positionNormalized);
      return _labels.Remove(new PositionKey(positionNormalized, tol));
    }

    public void Add(
      ProgesiVariableSignature signature,
      double positionNormalized,
      ProgesiAxisStationSide side = ProgesiAxisStationSide.None,
      double tol = DefaultTolerance)
    {
      if (!StringComparer.Ordinal.Equals(signature.Name, Name))
        throw new InvalidOperationException($"Signature.Name '{signature.Name}' does not match axis series Name '{Name}'.");

      if (!StringComparer.Ordinal.Equals(signature.ValueTypeKey, ValueTypeKey))
        throw new InvalidOperationException($"Signature.ValueTypeKey '{signature.ValueTypeKey}' does not match axis series ValueTypeKey '{ValueTypeKey}'.");

      AddUnsafe(positionNormalized, signature.Id, side, tol);
    }

    internal void AddUnsafe(
      double positionNormalized,
      int variableId,
      ProgesiAxisStationSide side = ProgesiAxisStationSide.None,
      double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(positionNormalized);

      var key = new StationKey(new PositionKey(positionNormalized, tol), side);
      if (!_map.TryGetValue(key, out var set))
      {
        set = new HashSet<int>();
        _map[key] = set;
      }

      set.Add(variableId);
    }

    public bool Move(
      double fromPositionNormalized,
      double toPositionNormalized,
      int variableId,
      ProgesiAxisStationSide side = ProgesiAxisStationSide.None,
      double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(fromPositionNormalized);
      ValidateNormalizedPosition(toPositionNormalized);

      var fromKey = new StationKey(new PositionKey(fromPositionNormalized, tol), side);
      if (!_map.TryGetValue(fromKey, out var set) || !set.Remove(variableId))
        return false;

      if (set.Count == 0) _map.Remove(fromKey);

      var toKey = new StationKey(new PositionKey(toPositionNormalized, tol), side);
      if (!_map.TryGetValue(toKey, out var toSet))
      {
        toSet = new HashSet<int>();
        _map[toKey] = toSet;
      }

      toSet.Add(variableId);
      return true;
    }

    public bool RemoveAt(
      double positionNormalized,
      int variableId,
      ProgesiAxisStationSide side = ProgesiAxisStationSide.None,
      double tol = DefaultTolerance)
    {
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidateNormalizedPosition(positionNormalized);

      var key = new StationKey(new PositionKey(positionNormalized, tol), side);
      if (!_map.TryGetValue(key, out var set)) return false;

      bool removed = set.Remove(variableId);
      if (removed && set.Count == 0) _map.Remove(key);
      return removed;
    }

    public void ReplaceMap(
      IEnumerable<(double positionNormalized, IEnumerable<int> ids, ProgesiAxisStationSide side)> entries,
      double tol = DefaultTolerance)
    {
      Guard.Against.Null(entries, nameof(entries));

      var newMap = new SortedDictionary<StationKey, HashSet<int>>();
      foreach (var entry in entries)
      {
        double pos = entry.positionNormalized;
        IEnumerable<int> ids = entry.ids;
        var side = entry.side;

        ValidateNormalizedPosition(pos);
        Guard.Against.Null(ids, nameof(entries));

        var key = new StationKey(new PositionKey(pos, tol), side);
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

    public void ReplaceLabels(IEnumerable<(double positionNormalized, string label)> entries, double tol = DefaultTolerance)
    {
      Guard.Against.Null(entries, nameof(entries));

      _labels.Clear();
      foreach (var entry in entries)
      {
        ValidateNormalizedPosition(entry.positionNormalized);
        if (string.IsNullOrWhiteSpace(entry.label))
          continue;
        _labels[new PositionKey(entry.positionNormalized, tol)] = entry.label.Trim();
      }
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

    public void SetCurvePayload(string? curvePayload)
    {
      CurvePayload = curvePayload ?? string.Empty;
    }

    public void SetMode(AxisCurveMode mode)
    {
      Mode = mode;
    }

    public void SetKeyPoints(IEnumerable<double>? keyPoints)
    {
      KeyPoints = NormalizeKeyPoints(keyPoints);
    }

    public void SetFunctionRef(ProgesiFunctionRef? functionRef)
    {
      FunctionRef = functionRef ?? ProgesiFunctionRef.Empty;
    }

    public double ToNormalizedFromReal(double realStation)
    {
      if (!AxisLength.HasValue)
        throw new InvalidOperationException("AxisLength is required to convert from real to normalized.");
      if (double.IsNaN(realStation) || double.IsInfinity(realStation))
        throw new ArgumentOutOfRangeException(nameof(realStation), "Station must be a finite number.");
      return realStation / AxisLength.Value;
    }

    public double ToRealFromNormalized(double normalizedStation)
    {
      if (!AxisLength.HasValue)
        throw new InvalidOperationException("AxisLength is required to convert from normalized to real.");
      ValidateNormalizedPosition(normalizedStation);
      return normalizedStation * AxisLength.Value;
    }

    private static IReadOnlyList<double> NormalizeKeyPoints(IEnumerable<double>? keyPoints)
    {
      if (keyPoints == null)
        return Array.Empty<double>();

      var list = new List<double>();
      foreach (var point in keyPoints)
      {
        ValidateNormalizedPosition(point);
        list.Add(point);
      }

      return list.OrderBy(x => x).Distinct().ToArray();
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
      yield return CurvePayload;
      yield return Mode;
      foreach (double kp in KeyPoints)
        yield return kp;
      yield return Name;
      yield return ValueTypeKey;
      yield return RuleId.HasValue ? RuleId.Value : int.MinValue;
      if (!FunctionRef.IsEmpty)
        yield return FunctionRef;

      foreach (var kv in _map)
      {
        yield return kv.Key.Position.Value;
        yield return (int)kv.Key.Side;
        foreach (int vid in kv.Value.OrderBy(x => x))
          yield return vid;
      }

      foreach (var kv in _labels)
      {
        yield return kv.Key.Value;
        yield return kv.Value;
      }
    }

    private readonly struct StationKey : IComparable<StationKey>, IEquatable<StationKey>
    {
      public PositionKey Position { get; }
      public ProgesiAxisStationSide Side { get; }

      public StationKey(PositionKey position, ProgesiAxisStationSide side)
      {
        Position = position;
        Side = side;
      }

      public int CompareTo(StationKey other)
      {
        int cmp = Position.CompareTo(other.Position);
        if (cmp != 0) return cmp;
        return Side.CompareTo(other.Side);
      }

      public bool Equals(StationKey other) => Position.Equals(other.Position) && Side == other.Side;
      public override bool Equals(object obj) => obj is StationKey sk && Equals(sk);
      public override int GetHashCode()
      {
        unchecked
        {
          return (Position.GetHashCode() * 397) ^ (int)Side;
        }
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
