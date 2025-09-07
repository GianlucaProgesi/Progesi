using System.Data.Common;
using System.Data.SQLite;  // <-- provider per .NET Framework
using Xunit;
using ProgesiCore.Persistence;

namespace ProgesiCore.Tests
{
    public class ProgesiAxisVariableRepositoryIntegrationTests
    {
        private static DbConnection CreateInMemoryDb()
        {
            // In-memory per System.Data.SQLite: vive finché la connessione resta aperta
            var conn = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            conn.Open();
            return conn;
        }

        [Fact]
        public void Save_ThenLoad_RoundTrip_PreservesState()
        {
            using var conn = CreateInMemoryDb();
            var repo = new ProgesiAxisVariableRepository(conn);

            // crea schema
            repo.EnsureSchema();

            var original = new ProgesiCore.ProgesiAxisVariable(42, "AX-INTEGRATION", 20.0, 5);
            original.Add("A", 0.0, 1);
            original.Add("A", 1.0, 2);
            original.Add("B", 2.5, 3);

            // salva
            repo.Save(original);

            // carica
            var loaded = repo.Load(42);

            // verifica round-trip
            Assert.True(original.Equals(loaded));
            Assert.Equal(original.GetHashCode(), loaded.GetHashCode());
        }
    }
}
