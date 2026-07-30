using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Progesi.Infrastructure.EF.Internal;

/// <summary>
/// Retries transient SQLite busy/locked errors under WAL contention, mirroring direct SQLite repos.
/// </summary>
public sealed class SqliteBusyRetryExecutionStrategy : ExecutionStrategy
{
  public SqliteBusyRetryExecutionStrategy(ExecutionStrategyDependencies dependencies)
      : base(dependencies, maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(1))
  {
  }

  protected override bool ShouldRetryOn(Exception exception)
  {
    return exception is SqliteException sqlite
        && (sqlite.SqliteErrorCode == 5 /* SQLITE_BUSY */ || sqlite.SqliteErrorCode == 6 /* SQLITE_LOCKED */);
  }
}
