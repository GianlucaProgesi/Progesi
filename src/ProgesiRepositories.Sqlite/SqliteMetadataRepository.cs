#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
    public sealed class SqliteMetadataRepository : SqliteRepositoryBase, IMetadataRepository
    {
        public SqliteMetadataRepository(string dbPath, bool resetSchema = false) : base(dbPath, resetSchema)
        {
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var conn = OpenConnection();
            using (var cmd = conn.CreateCommand())
            {
                if (_resetSchema)
                {
                    cmd.CommandText = @"
DROP TABLE IF EXISTS Metadata;
CREATE TABLE Metadata (
    Id           INTEGER PRIMARY KEY,
    Json         TEXT NOT NULL,
    LastModified TEXT NOT NULL,
    ContentHash  TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Metadata_ContentHash ON Metadata(ContentHash);";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
    Id           INTEGER PRIMARY KEY,
    Json         TEXT NOT NULL,
    LastModified TEXT NOT NULL,
    ContentHash  TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Metadata_ContentHash ON Metadata(ContentHash);";
                    cmd.ExecuteNonQuery();
                }
            }

            // Migrazione idempotente su DB già esistenti
            EnsureContentHash(conn: OpenConnection(), table: "Metadata");
        }

        // ========================= CRUD =========================

        public async Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Json, LastModified FROM Metadata WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", id);

            using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            var json = r.GetString(0);
            var lastModifiedStr = r.GetString(1);

            var dto = JsonConvert.DeserializeObject<ProgesiMetadataDto>(json);
            if (dto is null) return null;

            var lastModified = DateTime.Parse(lastModifiedStr, null, DateTimeStyles.RoundtripKind);

            var refs = dto.References?.Select(s => new Uri(s, UriKind.RelativeOrAbsolute));
            var snips = dto.Snips?.Select(s => ProgesiSnip.Create(
                Convert.FromBase64String(s.ContentBase64 ?? string.Empty),
                s.MimeType ?? "application/octet-stream",
                s.Caption,
                string.IsNullOrWhiteSpace(s.Source) ? null : new Uri(s.Source, UriKind.RelativeOrAbsolute)));

            var meta = ProgesiMetadata.Create(
                dto.CreatedBy ?? string.Empty,
                dto.AdditionalInfo,
                refs,
                snips,
                lastModified, // ? dalla colonna DB
                id            // ? Id del record DB
            );

            return meta;
        }

        public async Task UpsertAsync(ProgesiMetadata metadata, CancellationToken ct = default)
        {
            if (metadata is null) throw new ArgumentNullException(nameof(metadata));

            // Calcola l’hash di contenuto ESATTAMENTE come il domain
            var contentHash = ProgesiHash.Compute(metadata);

            // Serializza il DTO “reale” (non canonico): il round-trip deve riprodurre il contenuto
            var dto = ProgesiMetadataDto.FromDomain(metadata);
            var json = JsonConvert.SerializeObject(dto);
            var lastMod = metadata.LastModified.ToUniversalTime().ToString("o");

            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            // 1) Esiste già un record con lo stesso contenuto?
            int existingId = await GetIdByHashAsync(conn, contentHash, ct);
            if (existingId > 0)
            {
                // Aggiorna solo LastModified del record “unico”
                using (var upd = conn.CreateCommand())
                {
                    upd.Transaction = tx;
                    upd.CommandText = "UPDATE Metadata SET LastModified=$lm WHERE Id=$id;";
                    upd.Parameters.AddWithValue("$lm", lastMod);
                    upd.Parameters.AddWithValue("$id", existingId);
                    await upd.ExecuteNonQueryAsync(ct);
                }

                // Non creare un secondo record con Id diverso ? test dedup “got2 == null”
                tx.Commit();
                return;
            }

            // 2) Non esiste ancora: inserisci
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;

                if (metadata.Id > 0)
                {
                    // Round-trip: quando l’Id è specificato e l’hash è nuovo, creiamo con quell’Id
                    ins.CommandText = @"
INSERT OR IGNORE INTO Metadata (Id, Json, LastModified, ContentHash)
VALUES ($id, $json, $lm, $h);";
                    ins.Parameters.AddWithValue("$id", metadata.Id);
                }
                else
                {
                    // Senza Id, lasciamo a SQLite l’autoincremento
                    ins.CommandText = @"
INSERT OR IGNORE INTO Metadata (Json, LastModified, ContentHash)
VALUES ($json, $lm, $h);";
                }

                ins.Parameters.AddWithValue("$json", json);
                ins.Parameters.AddWithValue("$lm", lastMod);
                ins.Parameters.AddWithValue("$h", contentHash);

                await ins.ExecuteNonQueryAsync(ct);
            }

            // (In caso di race parallela, se un altro writer ha inserito prima, l’INSERT OR IGNORE viene ignorata)
            tx.Commit();
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Metadata WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            var n = await cmd.ExecuteNonQueryAsync(ct);
            return n > 0;
        }

        public async Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default)
        {
            var list = new List<ProgesiMetadata>();
            using var conn = OpenConnection();

            var ids = new List<int>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id FROM Metadata ORDER BY Id LIMIT $take OFFSET $skip;";
                cmd.Parameters.AddWithValue("$take", take);
                cmd.Parameters.AddWithValue("$skip", skip);
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) ids.Add(r.GetInt32(0));
            }

            foreach (var id in ids)
            {
                var m = await GetAsync(id, ct);
                if (m != null) list.Add(m);
            }

            return list;
        }

        // ====================== Helpers ======================

        private static async Task<int> GetIdByHashAsync(SqliteConnection conn, string contentHash, CancellationToken ct)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Metadata WHERE ContentHash=$h;";
            cmd.Parameters.AddWithValue("$h", contentHash);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            if (scalar is null || scalar is DBNull) return 0;
            return Convert.ToInt32(scalar);
        }

        // -------------------- DTO --------------------

        private sealed class ProgesiMetadataDto
        {
            public int Id { get; set; }
            public DateTime LastModifiedUtc { get; set; }
            public string? CreatedBy { get; set; }
            public string? AdditionalInfo { get; set; }
            public List<string>? References { get; set; }
            public List<SnipDto>? Snips { get; set; }

            public static ProgesiMetadataDto FromDomain(ProgesiMetadata m)
            {
                return new ProgesiMetadataDto
                {
                    Id = m.Id,
                    LastModifiedUtc = m.LastModified.ToUniversalTime(),
                    CreatedBy = m.CreatedBy,
                    AdditionalInfo = m.AdditionalInfo,
                    References = m.References?.Select(u => u.ToString()).ToList(),
                    Snips = m.Snips?.Select(s => new SnipDto
                    {
                        Id = s.Id,
                        MimeType = s.MimeType,
                        Caption = s.Caption,
                        Source = s.Source,
                        ContentBase64 = Convert.ToBase64String(s.Content ?? Array.Empty<byte>())
                    }).ToList()
                };
            }
        }

        private sealed class SnipDto
        {
            public Guid Id { get; set; }
            public string MimeType { get; set; } = "image/png";
            public string Caption { get; set; } = string.Empty;
            public string? Source { get; set; }
            public string ContentBase64 { get; set; } = string.Empty;
        }
    }
}
