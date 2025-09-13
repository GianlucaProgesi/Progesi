using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore
{
  /// <summary>
  /// Contenitore ad asse per collezioni di ProgesiVariable raggruppate per "Name".
  /// Tiene solo gli Id delle variabili per restare leggero e disaccoppiato.
  /// Ogni "Name" ha un dizionario posizionale (chiave = posizione lungo asse) con uno o più Id associati.
  /// </summary>
  public sealed class ProgesiAxisVariable : ValueObject
  {
    public const double DefaultTolerance = 1e-6;

    public int Id { get; private set; }
    public string AxisName { get; private set; } = string.Empty;
    public double? AxisLength { get; private set; }
    public int? RuleId { get; private set; }

    // C#8: niente target-typed new()
    private readonly Dictionary<string, SortedDictionary<PositionKey, HashSet<int>>> _byName
        = new Dictionary<string, SortedDictionary<PositionKey, HashSet<int>>>(StringComparer.Ordinal);

    public ProgesiAxisVariable(int id, string axisName, double? axisLength = null, int? ruleId = null)
    {
      Guard.Against.Negative(id, nameof(id));
      Guard.Against.NullOrWhiteSpace(axisName, nameof(axisName));
      if (axisLength.HasValue) Guard.Against.NegativeOrZero(axisLength.Value, nameof(axisLength));

      Id = id;
      AxisName = axisName.Trim();
      AxisLength = axisLength;
      RuleId = ruleId;
    }

    public IReadOnlyCollection<string> VariableNames => _byName.Keys;

    public IReadOnlyDictionary<double, int[]> GetMap(string variableName)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      if (!_byName.TryGetValue(variableName, out var map))
        return new Dictionary<double, int[]>();

      var result = new Dictionary<double, int[]>();
      foreach (var kv in map)
      {
        result[kv.Key.Value] = kv.Value.OrderBy(x => x).ToArray();
      }
      return result;
    }

    public IReadOnlyCollection<int> GetAt(string variableName, double position, double tol = DefaultTolerance)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      var key = new PositionKey(position, tol);
      if (!_byName.TryGetValue(variableName, out var map)) return Array.Empty<int>();
      return map.TryGetValue(key, out var set) ? set.OrderBy(x => x).ToArray() : Array.Empty<int>();
    }

    public IEnumerable<(string variableName, double position, int variableId)> EnumerateAll()
    {
      foreach (var outer in _byName.OrderBy(k => k.Key, StringComparer.Ordinal))
      {
        string name = outer.Key;
        var map = outer.Value;

        foreach (var inner in map)
        {
          var posKey = inner.Key;
          var ids = inner.Value;
          foreach (int id in ids.OrderBy(x => x))
            yield return (name, posKey.Value, id);
        }
      }
    }

    public void Add(string variableName, double position, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidatePosition(position);

      var key = new PositionKey(position, tol);
      SortedDictionary<PositionKey, HashSet<int>> map;
      if (!_byName.TryGetValue(variableName, out map))
      {
        map = new SortedDictionary<PositionKey, HashSet<int>>();
        _byName[variableName] = map;
      }

      if (!map.TryGetValue(key, out var set))
      {
        set = new HashSet<int>();
        map[key] = set;
      }

      set.Add(variableId);
    }

    public bool Move(string variableName, double fromPosition, double toPosition, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidatePosition(fromPosition);
      ValidatePosition(toPosition);

      if (!_byName.TryGetValue(variableName, out var map)) return false;

      var fromKey = new PositionKey(fromPosition, tol);
      if (!map.TryGetValue(fromKey, out var set) || !set.Remove(variableId))
        return false;

      if (set.Count == 0) map.Remove(fromKey);

      var toKey = new PositionKey(toPosition, tol);
      if (!map.TryGetValue(toKey, out var toSet))
      {
        toSet = new HashSet<int>();
        map[toKey] = toSet;
      }

      toSet.Add(variableId);
      return true;
    }

    public bool RemoveAt(string variableName, double position, int variableId, double tol = DefaultTolerance)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      Guard.Against.Negative(variableId, nameof(variableId));
      ValidatePosition(position);

      if (!_byName.TryGetValue(variableName, out var map)) return false;

      var key = new PositionKey(position, tol);
      if (!map.TryGetValue(key, out var set)) return false;

      bool removed = set.Remove(variableId);
      if (removed && set.Count == 0) map.Remove(key);
      return removed;
    }

    public bool RemoveAll(string variableName)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      return _byName.Remove(variableName);
    }

    public bool RenameGroup(string fromVariableName, string toVariableName)
    {
      Guard.Against.NullOrWhiteSpace(fromVariableName, nameof(fromVariableName));
      Guard.Against.NullOrWhiteSpace(toVariableName, nameof(toVariableName));
      if (StringComparer.Ordinal.Equals(fromVariableName, toVariableName)) return false;

      if (!_byName.TryGetValue(fromVariableName, out var map)) return false;
      if (_byName.ContainsKey(toVariableName))
        throw new InvalidOperationException("Group '" + toVariableName + "' already exists.");

      _byName.Remove(fromVariableName);
      _byName[toVariableName] = map;
      return true;
    }

    public void ReplaceMap(string variableName, IEnumerable<(double position, IEnumerable<int> ids)> entries, double tol = DefaultTolerance)
    {
      Guard.Against.NullOrWhiteSpace(variableName, nameof(variableName));
      Guard.Against.Null(entries, nameof(entries));

      var newMap = new SortedDictionary<PositionKey, HashSet<int>>();
      foreach (var entry in entries)
      {
        double pos = entry.position;
        IEnumerable<int> ids = entry.ids;

        ValidatePosition(pos);
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

      _byName[variableName] = newMap;
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

    private void ValidatePosition(double position)
    {
      if (double.IsNaN(position) || double.IsInfinity(position))
        throw new ArgumentOutOfRangeException(nameof(position), "Position must be a finite number.");

      if (AxisLength.HasValue)
      {
        if (position < -DefaultTolerance || position > AxisLength.Value + DefaultTolerance)
          throw new ArgumentOutOfRangeException(nameof(position),
              "Position " + position + " is outside [0, " + AxisLength.Value + "] (± tol).");
      }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
      yield return Id;
      yield return AxisName;
      yield return AxisLength.HasValue ? AxisLength.Value : double.NaN;
      yield return RuleId.HasValue ? RuleId.Value : int.MinValue;

      var orderedKeys = new List<string>(_byName.Keys);
      orderedKeys.Sort(StringComparer.Ordinal);

      foreach (var name in orderedKeys)
      {
        yield return name;
        var map = _byName[name];

        foreach (var kv in map)
        {
          yield return kv.Key.Value;
          foreach (int vid in kv.Value.OrderBy(x => x))
            yield return vid;
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
