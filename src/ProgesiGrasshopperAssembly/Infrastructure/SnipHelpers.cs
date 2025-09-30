using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Utility per normalizzare input (path/url/base64/snip:) in una stringa SNIP:
  /// "snip:{index}:{mime}:caption={caption}".
  /// Compatibile C# 8.0 (null-safe).
  /// </summary>
  public static class SnipHelpers
  {
    // data:image/png;base64,AAA...
    private static readonly Regex DataUrlRx = new Regex(
        @"^data:(?<mime>[-\w\.\+\/]+);base64,(?<b64>[A-Za-z0-9\+\/=]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Restituisce un MIME ragionevole a partire da path o URL.</summary>
    public static string GuessMimeFromPathOrUrl(string pathOrUrl)
    {
      if (string.IsNullOrEmpty(pathOrUrl))
        return "application/octet-stream";

      string ext = string.Empty;
      try
      {
        // Path.GetExtension può restituire null: normalizziamo sempre a stringa.
        var maybeExt = Path.HasExtension(pathOrUrl) ? Path.GetExtension(pathOrUrl) : null;
        ext = (maybeExt ?? string.Empty).TrimStart('.').ToLowerInvariant();
      }
      catch
      {
        ext = string.Empty; // URL strani ecc.
      }

      switch (ext)
      {
        case "png": return "image/png";
        case "jpg":
        case "jpeg": return "image/jpeg";
        case "bmp": return "image/bmp";
        case "gif": return "image/gif";
        case "webp": return "image/webp";
        case "tif":
        case "tiff": return "image/tiff";
        default: return "application/octet-stream";
      }
    }

    /// <summary>
    /// Prova a creare una SNIP string partendo dall'input GH.
    /// - Se input è "snip:*" → ricompone usando index+caption passati.
    /// - Se è data URL base64 → usa il mime catturato.
    /// - Se è path/URL immagine → deduce il mime dall’estensione.
    /// - Se sembra base64 grezzo → assume image/png.
    /// </summary>
    public static bool TryMake(object input, string caption, int index, out string snip, out string info)
    {
      snip = string.Empty;
      info = string.Empty;

      if (input == null)
      {
        info = "Input snip nullo";
        return false;
      }

      // Sanifica subito in non-null così il compilatore è felice in tutti i rami.
      var s = (input as string) ?? string.Empty;
      if (string.IsNullOrWhiteSpace(s))
      {
        info = "Input snip vuoto";
        return false;
      }
      s = s.Trim();

      // 1) già una snip:
      if (s.StartsWith("snip:", StringComparison.OrdinalIgnoreCase))
      {
        var parts = s.Split(':');
        var mime = (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
            ? parts[2]
            : "application/octet-stream";

        snip = Compose(index, mime, caption);
        return true;
      }

      // 2) data URL base64
      var m = DataUrlRx.Match(s);
      if (m.Success)
      {
        var mime = m.Groups["mime"].Success ? m.Groups["mime"].Value : "application/octet-stream";
        snip = Compose(index, mime, caption);
        return true;
      }

      // 3) path/URL (se ricaviamo un mime diverso dal default, lo accettiamo)
      var mimeFromPath = GuessMimeFromPathOrUrl(s);
      if (!string.Equals(mimeFromPath, "application/octet-stream", StringComparison.Ordinal))
      {
        snip = Compose(index, mimeFromPath, caption);
        return true;
      }

      // 4) base64 “nudo”
      if (LooksLikeBase64(s))
      {
        snip = Compose(index, "image/png", caption);
        return true;
      }

      info = "Formato Snip non riconosciuto";
      return false;
    }

    private static string Compose(int index, string mime, string caption)
    {
      // Normalizza sempre a stringa non-null
      var cap = (caption ?? string.Empty)
          .Replace("\r", " ")
          .Replace("\n", " ")
          .Trim();

      // evita i due punti nella caption
      cap = cap.Replace(":", "-");
      if (cap.Length > 120) cap = cap.Substring(0, 120);

      return "snip:" + index + ":" + (mime ?? "application/octet-stream") + ":caption=" + cap;
    }

    private static bool LooksLikeBase64(string s)
    {
      // accetta solo stringhe non vuote, lunghezza multipla di 4, charset base64
      if (string.IsNullOrEmpty(s)) return false;
      if ((s.Length % 4) != 0) return false;

      int pad = 0;
      for (int i = 0; i < s.Length; i++)
      {
        char c = s[i];
        if ((c >= 'A' && c <= 'Z') ||
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') ||
            c == '+' || c == '/')
        {
          continue;
        }

        if (c == '=')
        {
          pad++;
          continue;
        }

        return false;
      }
      return pad <= 2;
    }
  }
}
