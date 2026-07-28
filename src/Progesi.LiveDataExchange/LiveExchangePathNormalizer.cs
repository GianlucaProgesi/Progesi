using System;
using System.Collections.Generic;
using System.IO;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangePathNormalizer
  {
    public static string NormalizeExcelExportPath(string inPath)
    {
      string p = (inPath ?? "").Trim();
      if (string.IsNullOrEmpty(p))
      {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(home)) home = AppDomain.CurrentDomain.BaseDirectory;
        p = Path.Combine(home, "Progesi_Export.xlsx");
      }
      if (Directory.Exists(p)) p = Path.Combine(p, "Progesi_Export.xlsx");
      if (Path.GetExtension(p).Length == 0) p = p.TrimEnd(' ', '.') + ".xlsx";
      return p;
    }

    public static string NormalizeSqliteExportPath(string inPath)
    {
      string p = (inPath ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(p))
      {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(desktop)) desktop = AppDomain.CurrentDomain.BaseDirectory;
        p = Path.Combine(desktop, "Progesi_Export.db");
      }
      if (Directory.Exists(p))
        p = Path.Combine(p, "Progesi_Export.db");
      return p;
    }

    public static string PrepareSqliteExportPath(string p, bool overwrite)
    {
      if (File.Exists(p))
      {
        if (!overwrite)
          throw new InvalidOperationException("Il file esiste già: " + p);

        try
        {
          using (var fs = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
          File.Delete(p);
        }
        catch
        {
          var dir = Path.GetDirectoryName(p) ?? ".";
          var name = Path.GetFileNameWithoutExtension(p);
          var ext = Path.GetExtension(p);
          var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
          p = Path.Combine(dir, $"{name}_{stamp}{ext}");
        }
      }

      return p;
    }
  }
}
