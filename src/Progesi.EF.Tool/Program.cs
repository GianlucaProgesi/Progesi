using System;
using System.Globalization;
using Progesi.Data.EF;
using System.Linq;
using System.IO;

namespace Progesi.EF.Tool
{
  internal static class Program
  {
    static int Main(string[] args)
    {
      try
      {
        if (args.Length < 2)
        {
          Console.WriteLine("Usage:");
          Console.WriteLine("  Progesi.EF.Tool export \"path.db\"");
          Console.WriteLine("  Progesi.EF.Tool import \"path.db\" [--dry-run] [--strict]");
          return 2;
        }

        var cmd = (args[0] ?? "").Trim().ToLowerInvariant();
        var db  = args[1];

        bool dry = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        bool strict = args.Any(a => string.Equals(a, "--strict", StringComparison.OrdinalIgnoreCase));

        if (cmd == "export")
        {
          return DoExport(db);
        }
        if (cmd == "import")
        {
          return DoImport(db, strict, dry);
        }

        Console.WriteLine("Unknown command.");
        return 2;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine("ERROR: " + ex);
        return 1;
      }
    }

    static int DoExport(string dbPath)
    {
      if (string.IsNullOrWhiteSpace(dbPath)) { Console.WriteLine("path missing"); return 2; }
      if (Directory.Exists(dbPath)) dbPath = Path.Combine(dbPath, "Progesi_EF.db");
      if (File.Exists(dbPath)) File.Delete(dbPath);

      using (var ctx = new ProgesiDbContext(dbPath))
      {
        ctx.Database.CreateIfNotExists();

        // Qui potresti scegliere come fonte i tuoi file (es. un JSON export) — per ora mock o da Rhino via stdin.
        // Poiché il tool è stand-alone, la parte "leggi da Rhino StringTable" non è disponibile.
        // Per chiudere S2-C/1, questo tool serve solo a dimostrare che EF funziona out-of-proc.
      }

      Console.WriteLine("OK export (created empty db): " + dbPath);
      return 0;
    }

    static int DoImport(string dbPath, bool strict, bool dry)
    {
      if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath)) { Console.WriteLine("db not found"); return 2; }

      int meta = 0, vars = 0;
      using (var ctx = new ProgesiDbContext(dbPath))
      {
        if (!ctx.Database.Exists()) { Console.WriteLine("db not exists"); return 2; }
        meta = ctx.Metadata.Count();
        vars = ctx.Variables.Count();
      }

      Console.WriteLine($"OK import (probe) Meta={meta} Vars={vars} Strict={strict} Dry={dry}");
      return 0;
    }
  }
}
