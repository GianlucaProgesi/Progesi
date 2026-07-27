using SQLitePCL;

namespace Progesi.Stress.Tests;

internal static class SqliteTestBootstrap
{
  static SqliteTestBootstrap()
  {
    raw.SetProvider(new SQLite3Provider_e_sqlite3());
    Batteries_V2.Init();
  }

  internal static void EnsureInitialized() { }
}
