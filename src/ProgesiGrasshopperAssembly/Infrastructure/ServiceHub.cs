using System;
using System.IO;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Punto unico di accesso a repository reali o mock.
  /// </summary>
  internal static class ServiceHub
  {
    // Env keys supportate
    private static readonly string[] MockOnKeys = new[]
    {
            "PROGESI_MOCK_ON",
            "PROGESI_ENABLE_MOCK"
        };

    private static readonly string[] MockRootKeys = new[]
    {
            "PROGESI_MOCK_ROOT",
            "PROGESI_METADATA_MOCK_ROOT",
            "PROGESI_METADATA_FIXTURES"
        };

    /// <summary>Ritorna true se l'utente ha forzato l’uso del mock via env.</summary>
    public static bool IsMockForced()
    {
      foreach (var k in MockOnKeys)
      {
        var v = Environment.GetEnvironmentVariable(k);
        if (string.IsNullOrWhiteSpace(v)) continue;
        v = v.Trim().ToLowerInvariant();
        if (v == "1" || v == "true" || v == "yes" || v == "on")
          return true;
      }
      return false;
    }

    /// <summary>Risoluzione della root dei file di mock (prima env valida vince).</summary>
    public static string? GetMockRoot()
    {
      foreach (var k in MockRootKeys)
      {
        var v = Environment.GetEnvironmentVariable(k);
        if (!string.IsNullOrWhiteSpace(v))
          return v;
      }
      return null;
    }

    /// <summary>
    /// Tenta di ottenere un "repository" reale (se presente), oppure – se forzato il mock – 
    /// restituisce il repository file-based. Info descrive la modalità attiva.
    /// </summary>
    public static bool TryGetMetadataRepository(out object? repoObj, out string info)
    {
      // 1) Se mock forzato, costruiamo subito il file repository
      if (IsMockForced())
      {
        var root = GetMockRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
          repoObj = null;
          info = "Mock attivo ma root non impostata (set PROGESI_MOCK_ROOT).";
          return true;
        }

        repoObj = new FileMockMetadataRepository(root!);
        info = $"Mock (root: {root})";
        return true;
      }

      // 2) Repo reale (qui si lasciano future integrazioni Rhino/SQLite)
      repoObj = null;
      info = "OK (nessun repo collegato)";
      return true;
    }
  }
}
