using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProgesiCore
{
  /// <summary>
  /// Parser puro (no Rhino/Excel/GH) per import Cluster da testo.
  /// Serve per hardening e test.
  /// </summary>
  public static class ClusterImportParser
  {
    /// <summary>
    /// Converte una stringa "VariableIds" (potenzialmente sporca) in lista di Id interi.
    /// Supporta separatori: , ; | ! spazio
    /// Gestisce NBSP
    /// Tratta '.' come separatore (perché in una lista di Id non vogliamo decimali).
    /// </summary>
    public static int[] ParseVariableIds(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return Array.Empty<int>();

      // NBSP -> space
      raw = raw.Replace('\u00A0', ' ').Trim();

      // In una lista di Id, il punto non ha senso come decimale: lo trattiamo come separatore.
      raw = raw.Replace('.', ',');

      var parts = raw.Split(new[] { ',', ';', '|', ' ', '!' }, StringSplitOptions.RemoveEmptyEntries);

      var ids = new List<int>();
      foreach (var p in parts)
      {
        var t = p.Trim();
        if (t.Length == 0) continue;

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
          ids.Add(id);
      }

      return ids.Distinct().OrderBy(x => x).ToArray();
    }

    /// <summary>
    /// Helper per il parsing di una riga cluster, dato un getter "cell(key)".
    /// keys devono essere già normalizzate (es. "ID", "NAME", "DESCRIPTION", "VARIABLEIDS", "HASH").
    /// </summary>
    public static bool TryParseClusterRow(
      Func<string, string> cell,
      out int id,
      out string name,
      out string desc,
      out int[] varIds,
      out string hash,
      out string warn)
    {
      id = 0;
      name = "";
      desc = "";
      varIds = Array.Empty<int>();
      hash = "";
      warn = "";

      if (cell == null)
      {
        warn = "cell getter is null";
        return false;
      }

      var rawId = (cell("ID") ?? "").Trim();
      if (!int.TryParse(rawId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || id <= 0)
      {
        warn = $"invalid Id='{rawId}'";
        return false;
      }

      name = (cell("NAME") ?? "").Trim();
      if (string.IsNullOrWhiteSpace(name))
        name = $"Cluster-{id}";

      desc = (cell("DESCRIPTION") ?? "").Trim();
      hash = (cell("HASH") ?? "").Trim();

      var rawVarIds = (cell("VARIABLEIDS") ?? "").Trim();
      varIds = ParseVariableIds(rawVarIds);

      if (varIds.Length == 0 && !string.IsNullOrWhiteSpace(rawVarIds))
        warn = $"empty/invalid VariableIds raw='{rawVarIds}' (Id={id})";

      return true;
    }
  }
}
