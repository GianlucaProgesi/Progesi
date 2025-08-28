using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
    public class RobustnessMetadataTests
    {
        private static string NewDbPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "progesi_tests");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"progesi_{Guid.NewGuid():N}.sqlite");
        }

        private static ProgesiMetadata NewMetadata(
            string createdBy = "usr",
            string additionalInfo = "info",
            DateTime? lastModifiedUtc = null,
            string[]? refs = null,
            ProgesiSnip[]? snips = null,
            int? id = null)
        {
            var uris = refs?.Select(s => new Uri(s, UriKind.RelativeOrAbsolute));
            return ProgesiMetadata.Create(
                createdBy,
                additionalInfo,
                uris,
                snips,
                lastModifiedUtc ?? DateTime.UtcNow,
                id
            );
        }

        private static ProgesiSnip NewSnip(byte[]? content = null, string mime = "image/png", string caption = "cap", Uri? source = null)
        {
            return ProgesiSnip.Create(
                content ?? new byte[] { 0x01, 0x02, 0x03 },
                mime,
                caption,
                source
            );
        }

        [Fact]
        public async Task Upsert_SameContent_Updates_LastModified_Monotonically()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            var t1 = DateTime.UtcNow;
            var m1 = NewMetadata(
                lastModifiedUtc: t1,
                refs: new[] { "https://a/", "https://b/" },
                snips: new[] { NewSnip() },
                id: 1);

            await repo.UpsertAsync(m1);

            // upsert con stesso contenuto ma LastModified più recente
            var t2 = t1.AddSeconds(3);
            var m2 = NewMetadata(
                lastModifiedUtc: t2,
                refs: new[] { "https://a/", "https://b/" },
                snips: new[] { NewSnip() },
                id: 999); // id diverso: NON deve creare una nuova riga

            await repo.UpsertAsync(m2);

            var all = await repo.ListAsync();
            Assert.Single(all);

            var only = all.Single();
            Assert.True(only.LastModified.ToUniversalTime() >= t2.ToUniversalTime(),
                $"Expected LastModified >= {t2:o} but was {only.LastModified:o}");
        }

        [Fact]
        public async Task Upsert_Deduplicates_When_Order_Differs()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            var s1 = NewSnip(caption: "s1");
            var s2 = NewSnip(caption: "s2");

            var mA = NewMetadata(
                refs: new[] { "https://a/", "https://b/" },
                snips: new[] { s1, s2 },
                id: 1);

            var mB = NewMetadata(
                // stesso insieme, ordine invertito
                refs: new[] { "https://b/", "https://a/" },
                snips: new[] { s2, s1 },
                id: 2);

            await repo.UpsertAsync(mA);
            await repo.UpsertAsync(mB);

            var all = await repo.ListAsync();
            Assert.Single(all);
        }

        [Fact]
        public async Task Unicode_And_RelativeUris_Roundtrip_Correctly()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            var emoji = "Titolo con emoji 😃 e accenti èàü";
            var caption = "riga1\nriga2 con àèì";
            var rel = new Uri("images/á.png", UriKind.Relative);

            var snip = NewSnip(caption: caption, source: rel);
            var m = NewMetadata(
                createdBy: emoji,
                additionalInfo: "info-Ü",
                refs: new[] { "https://example.com/über?q=è" },
                snips: new[] { snip },
                id: 10);

            await repo.UpsertAsync(m);

            var back = await repo.GetAsync(10);
            Assert.NotNull(back);

            Assert.Contains("emoji", back!.CreatedBy);
            Assert.Contains("è", back.CreatedBy);
            Assert.Equal("info-Ü", back.AdditionalInfo);

            var gotSnip = back.Snips!.Single();
            Assert.Equal(caption, gotSnip.Caption);

            // Source è string? nel dominio: verifichiamo che sia relativa
            Assert.False(string.IsNullOrEmpty(gotSnip.Source));
            var parsed = new Uri(gotSnip.Source!, UriKind.RelativeOrAbsolute);
            Assert.False(parsed.IsAbsoluteUri);
            Assert.Equal("images/á.png", parsed.ToString());
        }

        [Fact]
        public async Task Large_Snip_Roundtrip_Preserves_Content()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            // ~1 MiB di dati
            var big = new byte[1024 * 1024];
            new Random(1234).NextBytes(big);

            var snip = NewSnip(content: big, caption: "big");
            var m = NewMetadata(
                refs: new[] { "https://a/" },
                snips: new[] { snip },
                id: 77);

            await repo.UpsertAsync(m);

            var back = await repo.GetAsync(77);
            Assert.NotNull(back);

            var got = back!.Snips!.Single();
            Assert.Equal("big", got.Caption);
            Assert.True(big.SequenceEqual(got.Content ?? Array.Empty<byte>()), "Il contenuto del blob non coincide dopo il round-trip.");
        }

        [Fact]
        public async Task Delete_Removes_Row_And_Allows_Reinsert_Without_Duplicates()
        {
            var db = NewDbPath();
            var repo = new SqliteMetadataRepository(db, resetSchema: true);

            var m = NewMetadata(
                refs: new[] { "https://a/", "https://b/" },
                snips: new[] { NewSnip() },
                id: 5);

            await repo.UpsertAsync(m);

            var got = await repo.GetAsync(5);
            Assert.NotNull(got);

            var deleted = await repo.DeleteAsync(5);
            Assert.True(deleted);

            var afterDelete = await repo.GetAsync(5);
            Assert.Null(afterDelete);

            // Reinserisco stesso contenuto (id diverso). Deve creare UNA sola riga.
            var m2 = NewMetadata(
                refs: new[] { "https://b/", "https://a/" }, // ordine diverso ma stesso set
                snips: new[] { NewSnip() },
                id: 6);

            await repo.UpsertAsync(m2);

            var all = await repo.ListAsync();
            Assert.Single(all);
        }
    }
}
