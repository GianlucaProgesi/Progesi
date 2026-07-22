using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  public sealed class AxisVarMapping
  {
    public Guid AxisGuid { get; }
    public string AxisName { get; }
    public string Name { get; }
    public string ValueTypeKey { get; }
    public IReadOnlyList<double> StationsNormalized { get; }
    public IReadOnlyList<string> VariableHashes { get; }

    public AxisVarMapping(Guid axisGuid, string axisName, string name, string valueTypeKey,
      IEnumerable<double> stationsNormalized, IEnumerable<string> variableHashes)
    {
      if (axisGuid == Guid.Empty) throw new ArgumentException("AxisGuid cannot be empty.", nameof(axisGuid));
      if (string.IsNullOrWhiteSpace(axisName)) throw new ArgumentException("AxisName is required.", nameof(axisName));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
      if (string.IsNullOrWhiteSpace(valueTypeKey)) throw new ArgumentException("ValueTypeKey is required.", nameof(valueTypeKey));
      if (stationsNormalized == null) throw new ArgumentNullException(nameof(stationsNormalized));
      if (variableHashes == null) throw new ArgumentNullException(nameof(variableHashes));

      var s = stationsNormalized.ToList();
      var h = variableHashes.Select(x => x ?? string.Empty).ToList();

      if (s.Count != h.Count)
        throw new InvalidOperationException($"StationsNormalized.Count ({s.Count}) must match VariableHashes.Count ({h.Count}).");

      for (int i = 0; i < s.Count; i++)
      {
        if (double.IsNaN(s[i]) || double.IsInfinity(s[i]) || s[i] < 0.0 || s[i] > 1.0)
          throw new ArgumentOutOfRangeException(nameof(stationsNormalized), $"Station[{i}] must be within [0,1].");
      }
      for (int i = 0; i < h.Count; i++)
      {
        if (string.IsNullOrWhiteSpace(h[i]))
          throw new ArgumentException($"Hash[{i}] is empty.", nameof(variableHashes));
      }

      AxisGuid = axisGuid;
      AxisName = axisName.Trim();
      Name = name.Trim();
      ValueTypeKey = valueTypeKey.Trim();
      StationsNormalized = s;
      VariableHashes = h;
    }

    public override string ToString() => $"{AxisName}:{Name} ({ValueTypeKey}) [{StationsNormalized.Count}]";
  }
}
