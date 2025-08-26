using SQLitePCL;

namespace ProgesiRepositories.Sqlite.Tests
{
    // Inizializza provider + libreria nativa una sola volta per l'assembly di test
    internal static class SqliteTestBootstrap
    {
        static SqliteTestBootstrap()
        {
            raw.SetProvider(new SQLite3Provider_e_sqlite3());
            Batteries_V2.Init();
        }

        public static void EnsureInitialized() { }
    }
}
