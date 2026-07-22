#nullable enable
using System;
using System.IO;
using Newtonsoft.Json;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Repository "finto" basato su file JSON nella cartella mock.
  /// Filenames supportati: con o senza .json (es. mock-00000001 / mock-00000001.json)
  /// </summary>
  internal sealed class FileMockMetadataRepository
  {
    private readonly string _root;

    public FileMockMetadataRepository(string mockDir)
    {
      if (string.IsNullOrWhiteSpace(mockDir))
        throw new ArgumentException("Percorso mock non valido.", nameof(mockDir));

      _root = mockDir;
    }

    private static string EnsureJson(string nameOrPath)
    {
      if (nameOrPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        return nameOrPath;
      return nameOrPath + ".json";
    }

    private string Resolve(string key)
    {
      var candidate = Path.Combine(_root, key);
      if (File.Exists(candidate)) return candidate;

      var withJson = Path.Combine(_root, EnsureJson(key));
      return withJson;
    }

    public bool TryGetByHash(string hash, out MockMetadata? m, out string info)
    {
      m = null;
      info = string.Empty;
      try
      {
        var path = Resolve(hash);
        if (!File.Exists(path))
        {
          info = $"Mock non trovato: {hash}";
          return false;
        }

        var json = File.ReadAllText(path);
        m = JsonConvert.DeserializeObject<MockMetadata>(json);
        if (m == null)
        {
          info = $"JSON non valido: {hash}";
          return false;
        }

        info = "OK (mock)";
        return true;
      }
      catch (Exception ex)
      {
        info = $"Errore mock: {ex.Message}";
        m = null;
        return false;
      }
    }

    public bool TryGetById(int id, out MockMetadata? m, out string info)
    {
      // Convenzione: il file di id N è "mock-0000000N.json"
      var hashLike = $"mock-{id:D8}";
      return TryGetByHash(hashLike, out m, out info);
    }
  }

  /// <summary>Schema minimo atteso nei JSON dei mock.</summary>
  internal sealed class MockMetadata
  {
    public int Id { get; set; }
    public string? Hash { get; set; }
    public string? By { get; set; }
    public string[]? Refs { get; set; }
    public string[]? Snips { get; set; }
    public DateTime? LastModified { get; set; }
  }
}
