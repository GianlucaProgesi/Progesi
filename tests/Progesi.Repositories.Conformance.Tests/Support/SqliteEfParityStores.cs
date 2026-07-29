using ProgesiCore;
using Progesi.Infrastructure.EF.Repositories;
using ProgesiRepositories.Sqlite;

namespace Progesi.Repositories.Conformance.Tests.Support;

internal sealed class SqliteEfVariableParityStores : IDisposable
{
  private readonly string _sqlitePath;
  private readonly string _efConnectionString;

  public IVariableRepository Sqlite { get; }
  public IVariableRepository Ef { get; }

  public SqliteEfVariableParityStores()
  {
    SqliteTestBootstrap.EnsureInitialized();
    _sqlitePath = Path.Combine(Path.GetTempPath(), $"progesi_parity_sqlite_{Guid.NewGuid():N}.sqlite");
    _efConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"progesi_parity_ef_{Guid.NewGuid():N}.sqlite")}";

    Sqlite = new SqliteVariableRepository(_sqlitePath, resetSchema: true);
    Ef = new EfVariableRepository(_efConnectionString, resetSchema: true);
  }

  public void Dispose()
  {
    TryDelete(_sqlitePath);
    TryDelete(_efConnectionString.Replace("Data Source=", string.Empty));
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}

internal sealed class SqliteEfMetadataParityStores : IDisposable
{
  private readonly string _sqlitePath;
  private readonly string _efConnectionString;

  public IMetadataRepository Sqlite { get; }
  public IMetadataRepository Ef { get; }

  public SqliteEfMetadataParityStores()
  {
    SqliteTestBootstrap.EnsureInitialized();
    _sqlitePath = Path.Combine(Path.GetTempPath(), $"progesi_parity_meta_sqlite_{Guid.NewGuid():N}.db");
    _efConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"progesi_parity_meta_ef_{Guid.NewGuid():N}.sqlite")}";

    Sqlite = new SqliteMetadataRepository(_sqlitePath, resetSchema: true);
    Ef = new EfMetadataRepository(_efConnectionString, resetSchema: true);
  }

  public void Dispose()
  {
    TryDelete(_sqlitePath);
    TryDelete(_efConnectionString.Replace("Data Source=", string.Empty));
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}

internal sealed class SqliteEfClusterParityStores : IDisposable
{
  private readonly string _sqlitePath;
  private readonly string _efConnectionString;

  public IProgesiVariableClusterRepository Sqlite { get; }
  public IProgesiVariableClusterRepository Ef { get; }

  public SqliteEfClusterParityStores()
  {
    SqliteTestBootstrap.EnsureInitialized();
    _sqlitePath = Path.Combine(Path.GetTempPath(), $"progesi_parity_cluster_sqlite_{Guid.NewGuid():N}.sqlite");
    _efConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"progesi_parity_cluster_ef_{Guid.NewGuid():N}.sqlite")}";

    Sqlite = new SqliteClusterRepository(_sqlitePath, resetSchema: true);
    Ef = new EfClusterRepository(_efConnectionString, resetSchema: true);
  }

  public void Dispose()
  {
    TryDelete(_sqlitePath);
    TryDelete(_efConnectionString.Replace("Data Source=", string.Empty));
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}
