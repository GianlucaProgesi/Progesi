using System;
using System.Collections.Generic;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelColumnMap
  {
    public static Dictionary<string, int> ResolveColumns(
      Dictionary<string, int> header,
      Dictionary<string, HashSet<string>> aliases)
    {
      if (header == null) throw new ArgumentNullException(nameof(header));
      if (aliases == null) throw new ArgumentNullException(nameof(aliases));

      var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      foreach (var pair in aliases)
      {
        string canonical = pair.Key;
        if (header.TryGetValue(canonical, out int column))
        {
          result[canonical] = column;
          continue;
        }

        foreach (var alternate in pair.Value)
        {
          if (header.TryGetValue(alternate, out column))
          {
            result[canonical] = column;
            break;
          }
        }
      }

      return result;
    }

    public static List<string> MissingRequired(Dictionary<string, int> map, IEnumerable<string> required)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      if (required == null) throw new ArgumentNullException(nameof(required));

      var missing = new List<string>();
      foreach (var requiredKey in required)
      {
        if (!map.ContainsKey(requiredKey))
          missing.Add(requiredKey);
      }

      return missing;
    }
  }
}
