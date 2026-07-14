using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProgesiCore
{
  /// <summary>
  /// Helper “puri” per ClusterOut (naming output dinamici + signature).
  /// Nessuna dipendenza da Rhino/Grasshopper.
  /// </summary>
  public static class ClusterOutNaming
  {
    public static string BuildSignature(int clusterId, IList<ProgesiVariable> varsInOrder)
    {
      var sb = new StringBuilder();
      sb.Append(clusterId).Append("|");

      // IMPORTANT: varsInOrder deve essere già nell'ordine del cluster
      for (int i = 0; i < varsInOrder.Count; i++)
      {
        var v = varsInOrder[i];
        sb.Append(v.Id).Append(":");
        sb.Append(string.IsNullOrWhiteSpace(v.Name) ? "" : v.Name);
        sb.Append(";");
      }

      return sb.ToString();
    }

    public static string SanitizeNick(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return "";

      var x = s.Trim();

      // spazi -> underscore
      x = Regex.Replace(x, @"\s+", "_");

      // caratteri non ammessi -> underscore
      x = Regex.Replace(x, @"[^A-Za-z0-9_\-]", "_");

      // evita nickname troppo lunghi
      if (x.Length > 40) x = x.Substring(0, 40);

      return x;
    }

    public static string MakeUnique(string nick, ISet<string> used)
    {
      if (used == null) throw new ArgumentNullException(nameof(used));
      if (string.IsNullOrWhiteSpace(nick)) nick = "Var";

      if (!used.Contains(nick))
      {
        used.Add(nick);
        return nick;
      }

      for (int i = 2; i < 999; i++)
      {
        var cand = $"{nick}_{i}";
        if (!used.Contains(cand))
        {
          used.Add(cand);
          return cand;
        }
      }

      var fb = $"Var_{Guid.NewGuid():N}".Substring(0, 40);
      used.Add(fb);
      return fb;
    }
  }
}
