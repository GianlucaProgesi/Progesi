using System;
using Microsoft.Data.Sqlite;

namespace ProgesiCore.Tests
{
  internal static class SqliteTestHelpers
  {
    /// <summary>
    /// Ritorna una connessione in-memory aperta per Microsoft.Data.Sqlite.
    /// NOTA: niente "Version=3", non è supportato.
    /// </summary>
    public static SqliteConnection CreateInMemoryDb()
    {
      // In-memory per-connection: perfetto se usi UNA connessione per tutto il test.
      // Se nel test apri più connessioni e vuoi condividerle, vedi commento più sotto.
      var conn = new SqliteConnection("Data Source=:memory:");
      conn.Open();

      // Se il test richiede schema/tabelle, crea qui le strutture.
      // Esempio:
      // using (var cmd = conn.CreateCommand())
      // {
      //     cmd.CommandText = "PRAGMA foreign_keys = ON;";
      //     cmd.ExecuteNonQuery();
      //     cmd.CommandText = "... CREATE TABLE IF NOT EXISTS ...";
      //     cmd.ExecuteNonQuery();
      // }

      return conn;
    }

    /// <summary>
    /// Alternativa per condividere la stessa DB in-memory tra più connessioni:
    /// usa (keeper, cs) = CreateSharedMemoryDb(); lascia "keeper" aperta
    /// finché durano le altre connessioni.
    /// </summary>
    public static (SqliteConnection keeper, string connectionString) CreateSharedMemoryDb()
    {
      // DB in RAM condiviso tra connessioni (Microsoft.Data.Sqlite 7+):
      // - Mode=Memory fa creare il DB in RAM
      // - Cache=Shared lo rende condivisibile da altre SqliteConnection con la stessa CS
      // - Data Source può essere un nome qualunque
      var cs = new SqliteConnectionStringBuilder
      {
        DataSource = "progesi-tests",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared
      }.ToString();

      var keeper = new SqliteConnection(cs);
      keeper.Open(); // mantiene vivo il DB finché resta aperta

      // Esempio creazione schema:
      // using (var cmd = keeper.CreateCommand())
      // {
      //     cmd.CommandText = "PRAGMA foreign_keys = ON;";
      //     cmd.ExecuteNonQuery();
      //     cmd.CommandText = "... CREATE TABLE IF NOT EXISTS ...";
      //     cmd.ExecuteNonQuery();
      // }

      return (keeper, cs);
    }
  }
}
