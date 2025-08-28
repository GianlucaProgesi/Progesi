using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
    public class LoggerInjectionTests
    {
        private sealed class TestLogger : IProgesiLogger
        {
            public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();

            public void Debug(string message) => Lines.Enqueue("DBG " + message);
            public void Info(string message) => Lines.Enqueue("INF " + message);
            public void Warn(string message) => Lines.Enqueue("WRN " + message);
            public void Error(string message, Exception? ex = null)
            {
                Lines.Enqueue("ERR " + message);
                if (ex != null) Lines.Enqueue("EX " + ex.GetType().Name);
            }
        }

        private static string NewDbPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "progesi_tests");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"progesi_{Guid.NewGuid():N}.sqlite");
        }

        private static ProgesiMetadata NewMetadata(int id) =>
            ProgesiMetadata.Create("usr", "info",
                new[] { new Uri("https://a/"), new Uri("https://b/") },
                new[] { ProgesiSnip.Create(new byte[] { 1, 2, 3 }, "image/png", "cap", null) },
                DateTime.UtcNow,
                id);

        [Fact]
        public async Task Repository_Logs_On_Open_And_Upsert()
        {
            var db = NewDbPath();
            var logger = new TestLogger();
            var repo = new SqliteMetadataRepository(db, resetSchema: true, logger: logger);

            // primo upsert => insert
            var id = await repo.UpsertAndGetIdAsync(NewMetadata(1));
            Assert.Equal(1, id);

            // ci aspettiamo almeno alcuni messaggi
            var lines = logger.Lines.ToArray();
            Assert.True(lines.Length > 0);

            // verifiche “deboli” sui prefissi/contesto (non troppo stringenti)
            Assert.Contains(lines, l => l.Contains("Opened connection"));
            Assert.Contains(lines, l => l.Contains("Upsert"));
        }
    }
}
