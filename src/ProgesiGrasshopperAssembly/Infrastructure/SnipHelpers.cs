#nullable disable
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Utility per Ref/Snip:
  ///  - TryNormalizeRef: valida path/URL e restituisce una forma normalizzata
  ///  - TryMake: normalizza Snip (data-url, base64 “nuda”, path/url, snip:*)
  /// </summary>
  public static class SnipHelpers
  {
    private static readonly Regex DataUrlRx = new Regex(
        @"^data:(?<mime>[-\w\.\+\/]+);base64,(?<b64>[A-Za-z0-9\+\/=]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryNormalizeRef(string input, out string normalized, out string reason)
    {
      normalized = string.Empty; reason = string.Empty;
      if (input == null) { reason = "Ref nullo"; return false; }

      string s = input.Trim();
      if (s.Length == 0) { reason = "Ref vuoto"; return false; }

      // data-url → lo accettiamo così com'è
      if (DataUrlRx.IsMatch(s)) { normalized = s; return true; }

      // path di file esistente (no schema, no data:)
      try
      {
        if (s.IndexOf("://", StringComparison.Ordinal) < 0 &&
            !s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
          string abs = Path.GetFullPath(s);
          if (File.Exists(abs)) { normalized = abs; return true; }
          reason = "File non trovato";
          return false;
        }
      }
      catch { /* ignore path errors */ }

      // URL http/https
      try
      {
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
          normalized = uri.ToString();
          return true;
        }
      }
      catch { }

      reason = "URL non valido";
      return false;
    }

    public static bool TryMake(object input, string caption, int index, out string snip, out string info)
    {
      snip = string.Empty; info = string.Empty;
      if (input == null) { info = "Input snip nullo"; return false; }

      var s = input as string;
      if (string.IsNullOrWhiteSpace(s)) { info = "Input snip vuoto"; return false; }
      s = s.Trim();

      // già snip
      if (s.StartsWith("snip:", StringComparison.OrdinalIgnoreCase))
      {
        string mime = "application/octet-stream";
        var parts = s.Split(':');
        if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])) mime = parts[2];
        snip = Compose(index, mime, caption);
        return true;
      }

      // data url
      var m = DataUrlRx.Match(s);
      if (m.Success)
      {
        string mime = m.Groups["mime"].Value;
        snip = Compose(index, mime, caption);
        return true;
      }

      // path/URL → deduce mime (e nel caso path verifica la presenza)
      var mimeFromPath = GuessMimeFromPathOrUrl(s);
      if (!string.Equals(mimeFromPath, "application/octet-stream", StringComparison.Ordinal))
      {
        if (s.IndexOf("://", StringComparison.Ordinal) < 0 &&
            !s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
          try
          {
            string abs = Path.GetFullPath(s);
            if (!File.Exists(abs)) { info = "File non trovato"; return false; }
          }
          catch { info = "Path non valido"; return false; }
        }
        snip = Compose(index, mimeFromPath, caption);
        return true;
      }

      // base64 “nuda”
      if (LooksLikeBase64(s))
      {
        snip = Compose(index, "image/png", caption);
        return true;
      }

      info = "Formato Snip non riconosciuto";
      return false;
    }

    public static string GuessMimeFromPathOrUrl(string pathOrUrl)
    {
      if (string.IsNullOrEmpty(pathOrUrl)) return "application/octet-stream";
      string ext = string.Empty;
      try
      {
        var maybeExt = Path.HasExtension(pathOrUrl) ? Path.GetExtension(pathOrUrl) : null;
        ext = (maybeExt ?? string.Empty).TrimStart('.').ToLowerInvariant();
      }
      catch { ext = string.Empty; }

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

    private static string Compose(int index, string mime, string caption)
    {
      if (caption == null) caption = string.Empty;
      caption = caption.Replace("\r", " ").Replace("\n", " ").Trim();
      caption = caption.Replace(":", "-");
      if (caption.Length > 120) caption = caption.Substring(0, 120);
      return "snip:" + index + ":" + (mime ?? "application/octet-stream") + ":caption=" + caption;
    }

    private static bool LooksLikeBase64(string s)
    {
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
        { continue; }

        if (c == '=') { pad++; continue; }
        return false;
      }
      return pad <= 2;
    }
  }
}
