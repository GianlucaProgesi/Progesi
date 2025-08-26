using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
DROP TABLE IF EXISTS MetadataSnips;
DROP TABLE IF EXISTS Metadata;

CREATE TABLE Metadata (
    Id             INTEGER PRIMARY KEY,
    LastModified   TEXT NOT NULL,
    CreatedBy      TEXT NOT NULL,
    AdditionalInfo TEXT NOT NULL,
    ReferencesJson TEXT,
    ContentHash    TEXT
);

CREATE TABLE MetadataSnips (
    Id           TEXT PRIMARY KEY,
    MetadataId   INTEGER NOT NULL,
    MimeType     TEXT NOT NULL,
    Caption      TEXT NOT NULL,
    Source       TEXT NULL,
    Content      BLOB NOT NULL,
    FOREIGN KEY(MetadataId) REFERENCES Metadata(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_MetadataSnips_MetadataId ON MetadataSnips(MetadataId);";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
    Id             INTEGER PRIMARY KEY,
    LastModified   TEXT NOT NULL,
    CreatedBy      TEXT NOT NULL,
    AdditionalInfo TEXT NOT NULL,
    ReferencesJson TEXT
);";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS MetadataSnips (
    Id           TEXT PRIMARY KEY,
    MetadataId   INTEGER NOT NULL,
    MimeType     TEXT NOT NULL,
    Caption      TEXT NOT NULL,
    Source       TEXT NULL,
    Content      BLOB NOT NULL,
    FOREIGN KEY(MetadataId) REFERENCES Metadata(Id) ON DELETE CASCADE
);";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"CREATE INDEX IF NOT EXISTS IX_MetadataSnips_MetadataId ON MetadataSnips(MetadataId);";
                    cmd.ExecuteNonQuery();

                    // migrazione non distruttiva
                    AddColumnIfMissing(conn, "Metadata", "ContentHash", "TEXT");
                }
            }

            EnsureContentHash(conn, "Metadata");
        }

        public async Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
        {
            using var conn = OpenConnection();

            ProgesiMetadata? meta = null;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, LastModified, CreatedBy, AdditionalInfo, ReferencesJson FROM Metadata WHERE Id=$id;";
                cmd.Parameters.AddWithValue("$id", id);

                using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) return null;

                var mid = r.GetInt32(0);
                var lastMod = DateTime.Parse(r.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                var createdBy = r.GetString(2);
                var addInfo = r.GetString(3);
                var refsJson = r.IsDBNull(4) ? "[]" : r.GetString(4);
                var refsStr = JsonConvert.DeserializeObject<List<string>>(refsJson) ?? new List<string>();
                var refUris = refsStr
                    .Select(s => { Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u); return u; })
                    .Where(u => u != null)
                    .Cast<Uri>();

                meta = ProgesiMetadata.Create(createdBy, addInfo, refUris, snips: null, lastModifiedUtc: lastMod, id: mid);
            }

            using (var snCmd = conn.CreateCommand())
            {
                snCmd.CommandText = "SELECT MimeType, Caption, Source, Content FROM MetadataSnips WHERE MetadataId=$id;";
                snCmd.Parameters.AddWithValue("$id", id);
                using var rs = await snCmd.ExecuteReaderAsync(ct);
                while (await rs.ReadAsync(ct))
                {
                    var mime = rs.GetString(0);
                    var caption = rs.GetString(1);
                    var source = rs.IsDBNull(2) ? null : rs.GetString(2);
                    var content = (byte[])rs["Content"];

                    Uri? srcUri = null;
                    if (!string.IsNullOrWhiteSpace(source))
                        Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out srcUri);

                    meta!.AddSnip(content, mime, caption, srcUri);
                }
            }

            return meta;
        }

        public async Task UpsertAsync(ProgesiMetadata meta, CancellationToken ct = default)
        {
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            var hash = ProgesiHash.Compute(meta);

            using (var find = conn.CreateCommand())
            {
                find.Transaction = tx;
                find.CommandText = "SELECT Id FROM Metadata WHERE ContentHash=$h LIMIT 1;";
                find.Parameters.AddWithValue("$h", hash);
                var obj = await find.ExecuteScalarAsync(ct);
                if (obj != null && obj != DBNull.Value)
                {
                    var existingId = Convert.ToInt32(obj);
                    if (existingId != meta.Id)
                    {
                        tx.Commit();
                        return;
                    }
                }
            }

            var refs = (meta.References ?? Array.Empty<Uri>()).Select(u => u.ToString()).ToArray();
            var refsJson = JsonConvert.SerializeObject(refs);

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Metadata (Id, LastModified, CreatedBy, AdditionalInfo, ReferencesJson, ContentHash)
VALUES ($id, $lm, $cb, $ai, $refs, $h)
ON CONFLICT(Id) DO UPDATE SET
  LastModified=excluded.LastModified,
  CreatedBy=excluded.CreatedBy,
  AdditionalInfo=excluded.AdditionalInfo,
  ReferencesJson=excluded.ReferencesJson,
  ContentHash=excluded.ContentHash;";
                cmd.Parameters.AddWithValue("$id", meta.Id);
                cmd.Parameters.AddWithValue("$lm", meta.LastModified.ToString("o"));
                cmd.Parameters.AddWithValue("$cb", meta.CreatedBy ?? string.Empty);
                cmd.Parameters.AddWithValue("$ai", meta.AdditionalInfo ?? string.Empty);
                cmd.Parameters.AddWithValue("$refs", refsJson ?? "[]");
                cmd.Parameters.AddWithValue("$h", hash);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM MetadataSnips WHERE MetadataId=$mid;";
                del.Parameters.AddWithValue("$mid", meta.Id);
                await del.ExecuteNonQueryAsync(ct);
            }

            foreach (var s in meta.Snips ?? Array.Empty<ProgesiSnip>())
            {
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO MetadataSnips (Id, MetadataId, MimeType, Caption, Source, Content)
VALUES ($id, $mid, $mt, $cap, $src, $cont);";
                ins.Parameters.AddWithValue("$id", s.Id.ToString());
                ins.Parameters.AddWithValue("$mid", meta.Id);
                ins.Parameters.AddWithValue("$mt", s.MimeType ?? "image/png");
                ins.Parameters.AddWithValue("$cap", s.Caption ?? string.Empty);
                ins.Parameters.AddWithValue("$src", (object?)s.Source ?? DBNull.Value);
                ins.Parameters.AddWithValue("$cont", s.Content ?? Array.Empty<byte>());
                await ins.ExecuteNonQueryAsync(ct);
            }

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
    }
}
