using System;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Strato di compatibilità tra i componenti GH e i vari repository (reali o mock).
  /// </summary>
  internal static class MetadataRepositoryCompatExtensions
  {
    /// <summary>
    /// Get: prima per Hash (se non vuoto), altrimenti per Id; ricade sul mock se forzato.
    /// </summary>
    public static bool TryGetByHashThenId(
        object? repoObj,
        string? hash,
        int id,
        out object? metadata,
        out string info)
    {
      metadata = null;
      info = string.Empty;

      // Se mock forzato, ignoriamo repoObj e leggiamo dai file
      if (ServiceHub.IsMockForced())
      {
        var rootInfo = ServiceHub.GetMockRoot();
        if (string.IsNullOrWhiteSpace(rootInfo))
        {
          info = "Mock attivo ma root non impostata (PROGESI_MOCK_ROOT).";
          return false;
        }

        var mock = new FileMockMetadataRepository(rootInfo!);
        if (!string.IsNullOrWhiteSpace(hash))
        {
          if (mock.TryGetByHash(hash!, out var m, out info))
          {
            metadata = m!;
            return true;
          }
          return false;
        }

        if (id > 0)
        {
          if (mock.TryGetById(id, out var m, out info))
          {
            metadata = m!;
            return true;
          }
          return false;
        }

        info = "Input non valido (hash/id).";
        return false;
      }

      // Qui andrebbe la chiamata al repo reale quando sarà disponibile
      info = "OK (nessun repo collegato)";
      metadata = null;
      return true;
    }

    public static bool TryUpsert(
        object? repoObj,
        object? payload,
        out object? persisted,
        out string info)
    {
      persisted = null;
      if (ServiceHub.IsMockForced())
      {
        info = "Upsert non supportato in modalità mock.";
        return false;
      }

      info = "OK (nessun repo collegato)";
      return true;
    }

    public static bool TryDelete(
        object? repoObj,
        int id,
        out string info)
    {
      if (ServiceHub.IsMockForced())
      {
        info = "Delete non supportato in modalità mock.";
        return false;
      }

      info = "OK (nessun repo collegato)";
      return true;
    }
  }
}
