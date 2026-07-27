using Progesi.Infrastructure.EF.Repositories;
using ProgesiCore;
using ProgesiRepositories.InMemory;
using ProgesiRepositories.Sqlite;

namespace Progesi.Stress.Tests;

public enum VariableStoreKind
{
  InMemory,
  Sqlite,
  Ef
}

internal sealed class StressVariableStore : IDisposable
{
  public VariableStoreKind Kind { get; }
  public IVariableRepository Repository { get; }
  private readonly string? _sqlitePath;
  private readonly string? _efConnectionString;

  private StressVariableStore(VariableStoreKind kind, IVariableRepository repo, string? sqlitePath, string? efConnectionString)
  {
    Kind = kind;
    Repository = repo;
    _sqlitePath = sqlitePath;
    _efConnectionString = efConnectionString;
  }

  internal static StressVariableStore Create(VariableStoreKind kind)
  {
    switch (kind)
    {
      case VariableStoreKind.InMemory:
        return new StressVariableStore(kind, new InMemoryVariableRepository(), null, null);

      case VariableStoreKind.Sqlite:
        SqliteTestBootstrap.EnsureInitialized();
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"progesi_stress_var_{Guid.NewGuid():N}.sqlite");
        return new StressVariableStore(kind, new SqliteVariableRepository(sqlitePath, resetSchema: true), sqlitePath, null);

      case VariableStoreKind.Ef:
        var cs = EfTestBootstrap.CreateTempFileConnectionString();
        return new StressVariableStore(kind, new EfVariableRepository(cs, resetSchema: true), null, cs);

      default:
        throw new ArgumentOutOfRangeException(nameof(kind));
    }
  }

  public void Dispose()
  {
    if (_sqlitePath != null)
    {
      try
      {
        if (File.Exists(_sqlitePath))
          File.Delete(_sqlitePath);
      }
      catch { /* best-effort */ }
    }

    if (_efConnectionString != null)
      EfTestBootstrap.TryDeleteFile(_efConnectionString);
  }
}

internal sealed class StressMetadataStore : IDisposable
{
  public IMetadataRepository Repository { get; }
  private readonly string? _sqlitePath;
  private readonly string? _efConnectionString;

  private StressMetadataStore(IMetadataRepository repo, string? sqlitePath, string? efConnectionString)
  {
    Repository = repo;
    _sqlitePath = sqlitePath;
    _efConnectionString = efConnectionString;
  }

  internal static StressMetadataStore CreateSqlite()
  {
    SqliteTestBootstrap.EnsureInitialized();
    var path = Path.Combine(Path.GetTempPath(), $"progesi_stress_meta_{Guid.NewGuid():N}.sqlite");
    return new StressMetadataStore(new SqliteMetadataRepository(path, resetSchema: true), path, null);
  }

  internal static StressMetadataStore CreateEf()
  {
    var cs = EfTestBootstrap.CreateTempFileConnectionString();
    return new StressMetadataStore(new EfMetadataRepository(cs, resetSchema: true), null, cs);
  }

  public void Dispose()
  {
    if (_sqlitePath != null)
    {
      try
      {
        if (File.Exists(_sqlitePath))
          File.Delete(_sqlitePath);
      }
      catch { /* best-effort */ }
    }

    if (_efConnectionString != null)
      EfTestBootstrap.TryDeleteFile(_efConnectionString);
  }
}
