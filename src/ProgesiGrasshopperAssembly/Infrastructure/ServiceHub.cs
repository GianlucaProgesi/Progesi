// ServiceHub.cs
#nullable disable
using System;
using System.IO;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Wiring dei servizi runtime per i componenti GH.
  /// Priorità: LIVE (sqlite) -> MOCK (file) -> NONE.
  /// </summary>
  internal static class ServiceHub
  {
    /// <summary>Context minimale per il LIVE SQLite (solo percorso del DB).</summary>
    internal sealed class LiveSqliteContext
    {
      public string DbPath { get; private set; }
      public LiveSqliteContext(string dbPath) { DbPath = dbPath ?? string.Empty; }
    }

    /// <summary>
    /// Ritorna un oggetto repository in base alle env var:
    ///   PROGESI_LIVE_ON=1 + PROGESI_LIVE_DB=<file>  -> LiveSqliteContext
    ///   PROGESI_MOCK_ON=1 + PROGESI_MOCK_ROOT=<dir> -> FileMockMetadataRepository
    /// altrimenti nessun repo collegato.
    /// </summary>
    public static bool TryGetMetadataRepository(out object repoObj, out string info)
    {
      // 1) LIVE (sqlite) – legge sia dal Process che dallo User (così non serve riavvio)
      var liveOn = ReadEnv("PROGESI_LIVE_ON");
      var liveDb = ReadEnv("PROGESI_LIVE_DB");
      if (IsTrue(liveOn) && !string.IsNullOrWhiteSpace(liveDb) && File.Exists(liveDb))
      {
        repoObj = new LiveSqliteContext(liveDb);
        info = $"LIVE (sqlite) → {liveDb}";
        return true;
      }

      // 2) MOCK (file)
      var mockOn = ReadEnv("PROGESI_MOCK_ON");
      var mockRoot = ReadEnv("PROGESI_MOCK_ROOT");
      if (IsTrue(mockOn) && !string.IsNullOrWhiteSpace(mockRoot) && Directory.Exists(mockRoot))
      {
        repoObj = new FileMockMetadataRepository(mockRoot);
        info = $"MOCK → {mockRoot}";
        return true;
      }

      // 3) Nessun repo
      repoObj = null;
      info = "OK (nessun repo collegato)";
      return false;
    }

    // ---------- helpers ----------

    private static string ReadEnv(string name)
    {
      // process -> user -> machine
      var p = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
      if (!string.IsNullOrEmpty(p)) return p;
      var u = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
      if (!string.IsNullOrEmpty(u)) return u;
      var m = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
      if (!string.IsNullOrEmpty(m)) return m;
      return string.Empty;
    }

    private static bool IsTrue(string v)
    {
      if (string.IsNullOrWhiteSpace(v)) return false;
      v = v.Trim();
      return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
  }
}
