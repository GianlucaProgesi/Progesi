#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Progesi.Grasshopper.Browsers.Internal
{
  /// <summary>
  /// Writer CSV semplice e robusto (UTF-8, senza BOM).
  /// Accetta headers come IEnumerable<string> e righe come IEnumerable<IEnumerable<string>>
  /// così vanno bene sia List&lt;string[]&gt; che List&lt;List&lt;string&gt;&gt;.
  /// </summary>
  internal static class CsvUtils
  {
    public static void Write(string path, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
      if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("Percorso CSV vuoto.", nameof(path));
      if (headers == null) throw new ArgumentNullException(nameof(headers));
      if (rows == null) throw new ArgumentNullException(nameof(rows));

      var full = Path.GetFullPath(path);
      var dir = Path.GetDirectoryName(full);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

      using (var sw = new StreamWriter(full, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
      {
        sw.WriteLine(Join(headers));
        foreach (var r in rows)
          sw.WriteLine(Join(r));
      }
    }

    private static string Join(IEnumerable<string> cells)
    {
      if (cells == null) return "";
      var buf = new List<string>();
      foreach (var c in cells) buf.Add(Escape(c ?? ""));
      return string.Join(",", buf);
    }

    private static string Escape(string s)
    {
      bool needQuote = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
      if (s.IndexOf('"') >= 0) s = s.Replace("\"", "\"\"");
      return needQuote ? $"\"{s}\"" : s;
    }
  }
}
