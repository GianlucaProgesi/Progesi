#nullable disable
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Utility per Ref/Snip:
  ///  - TryNormalizeRef: valida path/URL e restituisce una forma normalizzata
  ///  - TryMake: normalizza Snip (data-url, base64 “nuda”, path/url, snip:*, Bitmap, Uri)
  /// </summary>
  public static class SnipHelpers
  {
    private static readonly Regex DataUrlRx = new Regex(
        @"^data:(?<mime>[-\w\.\+\/]+);base64,(?<b64>[A-Za-z0-9\+\/=]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static bool IsAbsolutePath(string s)
    {
      try { return Path.IsPathRooted(s) && !s.StartsWith("~"); }
      catch { return false; }
    }

    public static bool TryNormalizeRef(string input, out string normalized, out string reason)
    {
      normalized = string.Empty; reason = string.Empty;
      if (string.IsNullOrWhiteSpace(input)) { reason = "Riferimento vuoto"; return false; }

      var s = input.Trim();

      // path assoluto
      if (IsAbsolutePath(s) || File.Exists(s))
      {
        try
        {
          normalized = Path.GetFullPath(s);
          return true;
        }
        catch (Exception ex) { reason = "Path non valido: " + ex.Message; return false; }
      }

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

      // Bitmap diretta
      if (input is Bitmap)
      {
        snip = Compose(index, "image/png", caption ?? "");
        info = "OK (bitmap)";
        return true;
      }

      // Uri diretta
      if (input is Uri u)
      {
        string mime = GuessMimeFromPathOrUrl(u.ToString());
        snip = Compose(index, mime, caption);
        return true;
      }

      // string (path/url/base64/data-url/snip:*)
      if (input is string sIn)
      {
        var s = sIn.Trim();

        // già snip
        if (s.StartsWith("snip:", StringComparison.OrdinalIgnoreCase))
        {
          string mime = "application/octet-stream";
          var parts = s.Split(':');
          if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])) mime = parts[2];
          snip = Compose(index, mime, caption);
          return true;
        }

        // data URL
        var m = DataUrlRx.Match(s);
        if (m.Success)
        {
          string mime = m.Groups["mime"].Value;
          snip = Compose(index, mime, caption);
          return true;
        }

        // base64 “nuda”
        if (LooksLikeBase64(s))
        {
          snip = Compose(index, "application/octet-stream", caption);
          return true;
        }

        // path locale (accetta anche non-rooted se esiste)
        if (IsAbsolutePath(s) || File.Exists(s))
        {
          string full = s;
          try { full = Path.GetFullPath(s); } catch { /* keep s */ }
          string mime = GuessMimeFromPathOrUrl(full);
          snip = Compose(index, mime, caption);
          return true;
        }

        // URL http/https
        try
        {
          if (Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
              (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
          {
            string mime = GuessMimeFromPathOrUrl(uri.ToString());
            snip = Compose(index, mime, caption);
            return true;
          }
        }
        catch { }
      }

      info = "Formato Snip non riconosciuto";
      return false;
    }

    private static bool LooksLikeBase64(string s)
    {
      if (string.IsNullOrEmpty(s)) return false;
      s = s.Trim();
      if (s.Length < 16) return false;
      foreach (char c in s)
      {
        if (!(char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=' || c == '\r' || c == '\n')) return false;
      }
      return true;
    }

    // snip placeholder con mime e caption
    public static string Compose(int index, string mime, string caption)
    {
      var cap = caption ?? "";
      return $"snip:{index}:{mime}|{cap}";
    }

    public static string GuessMimeFromPathOrUrl(string pathOrUrl)
    {
      if (string.IsNullOrEmpty(pathOrUrl)) return "application/octet-stream";
      string ext = string.Empty;
      try
      {
        var maybeExt = Path.HasExtension(pathOrUrl) ? Path.GetExtension(pathOrUrl) : string.Empty;
        ext = (maybeExt ?? "").Trim().ToLowerInvariant();
      }
      catch { }

      switch (ext)
      {
        case ".png": return "image/png";
        case ".jpg":
        case ".jpeg": return "image/jpeg";
        case ".gif": return "image/gif";
        case ".pdf": return "application/pdf";
        case ".svg": return "image/svg+xml";
        case ".webp": return "image/webp";
        default: return "application/octet-stream";
      }
    }
  }
}
