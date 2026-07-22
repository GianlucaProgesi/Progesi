using System;
using System.IO;
using ProgesiCore;
using ProgesiRepositories.Sqlite;

namespace Progesi.Repositories.Conformance.Tests
{
  public sealed class SqliteVariableRepositoryConformanceStore : IVariableRepositoryConformanceStore
  {
    private readonly string _dbPath;

    public string StoreName => "Sqlite";

    public IVariableRepository Repository { get; }

    public SqliteVariableRepositoryConformanceStore()
    {
      SqliteTestBootstrap.EnsureInitialized();
      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_conf_{Guid.NewGuid():N}.sqlite");
      Repository = new SqliteVariableRepository(_dbPath, resetSchema: true);
    }

    public void Dispose()
    {
      try
      {
        if (File.Exists(_dbPath))
          File.Delete(_dbPath);
      }
      catch
      {
        // best-effort temp cleanup
      }
    }
  }
}
