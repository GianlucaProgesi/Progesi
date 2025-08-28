using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
    public class ConcurrencyStressTests
    {
        private static string NewDbPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "progesi_tests");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"progesi_{Guid.NewGuid():N}.sqlite");
        }

        private static ProgesiMetadata MakeMeta(
            int id,
            byte[] bytes,
            string caption = "cap")
        {
            var snip = ProgesiSnip.Create(bytes, "image/png", caption, null);
            return ProgesiMetadata.Create(
                "usr",
                "info",
                new[] { new Uri("https://a/"), new Uri("https://b/") },
                new[] { snip },
                DateTime.UtcNow,
                id);
        }

        [Fact]
        public async Task Parallel_SameContent_OneRow_NoExceptions()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            // stesso contenuto per tutti i task
            var bytes = new byte[] { 1, 2, 3 };

            var tasks = new List<Task>();
            var rnd = new Random(123);
            int workers = 32;

            for (int i = 0; i < workers; i++)
            {
                var localI = i; // cattura
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(rnd.Next(0, 10));
                    // usa SEMPRE un Id valido (>0)
                    int id = localI + 1;
                    var m = MakeMeta(id, bytes, caption: "same");
                    await repo.UpsertAsync(m);
                }));
            }

            await Task.WhenAll(tasks);

            var all = await repo.ListAsync();
            Assert.Single(all);
            Assert.True(all[0].Id > 0, "L'Id finale deve essere valido (>0).");
        }

        [Fact]
        public async Task Parallel_DifferentContent_CountMatchesUnique()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            int workers = 25;
            var tasks = new List<Task>();

            for (int i = 0; i < workers; i++)
            {
                var localI = i;
                tasks.Add(Task.Run(async () =>
                {
                    // contenuto diverso: cambia i BYTE (non solo caption)
                    var bytes = new byte[] { 1, 2, (byte)(3 + localI) };
                    var m = MakeMeta(id: localI + 1, bytes, caption: $"cap-{localI}");
                    var id = await repo.UpsertAndGetIdAsync(m);
                    Assert.True(id > 0);
                }));
            }

            await Task.WhenAll(tasks);

            var all = await repo.ListAsync(skip: 0, take: 1000);
            Assert.Equal(workers, all.Count);
        }
    }
}
