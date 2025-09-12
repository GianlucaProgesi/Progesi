#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
  public sealed class SqliteMetadataRepository : SqliteRepositoryBase, IMetadataRepository
  {
    // Costruttore esistente (senza logger)
    public SqliteMetadataRepository(string dbPath, bool resetSchema = false)
        : base(dbPath, resetSchema)
    {
      EnsureSchema();
    }

    // ?? NUOVO: costruttore pubblico che accetta un logger
    public SqliteMetadataRepository(string dbPath, bool resetSchema, IProgesiLogger logger)
        : base(dbPath, resetSchema, logger)
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
);";
          cmd.ExecuteNonQuery();
          _log.Info("[SQLite] Recreated table 'Metadata' due to resetSchema=true.");
        }
        else
        {
          cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
    Id           INTEGER PRIMARY KEY,
    Json         TEXT NOT NULL,
    LastModified TEXT NOT NULL,
    ContentHash  TEXT NOT NULL
);";
          cmd.ExecuteNonQuery();
        }
      }

      EnsureSchemaInfoAndCleanup(conn, "Metadata");
    }

    // ========================= CRUD =========================

    public async Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
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
                  lastModified,
                  id
              );

        return meta;
      }, ct: ct);
    }

    public async Task UpsertAsync(ProgesiMetadata metadata, CancellationToken ct = default)
    {
      if (metadata is null) throw new ArgumentNullException(nameof(metadata));

      await WithRetryAsync(async () =>
      {
        var contentHash = ProgesiHash.Compute(metadata);

        var dto = ProgesiMetadataDto.FromDomain(metadata);
        var json = JsonConvert.SerializeObject(dto);
        var lastMod = metadata.LastModified.ToUniversalTime().ToString("o");

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        int existingId = await GetIdByHashAsync(conn, contentHash, ct);
        if (existingId > 0)
        {
          using (var upd = conn.CreateCommand())
          {
            upd.Transaction = tx;
            upd.CommandText = "UPDATE Metadata SET LastModified=$lm WHERE Id=$id;";
            upd.Parameters.AddWithValue("$lm", lastMod);
            upd.Parameters.AddWithValue("$id", existingId);
            await upd.ExecuteNonQueryAsync(ct);
          }

          tx.Commit();
          _log.Debug($"[SQLite] Upsert dedup: reused Id={existingId} for ContentHash={contentHash}.");
          return;
        }

        using (var ins = conn.CreateCommand())
        {
          ins.Transaction = tx;

          if (metadata.Id > 0)
          {
            ins.CommandText = @"
INSERT OR IGNORE INTO Metadata (Id, Json, LastModified, ContentHash)
VALUES ($id, $json, $lm, $h);";
            ins.Parameters.AddWithValue("$id", metadata.Id);
          }
          else
          {
            ins.CommandText = @"
INSERT OR IGNORE INTO Metadata (Json, LastModified, ContentHash)
VALUES ($json, $lm, $h);";
          }

          ins.Parameters.AddWithValue("$json", json);
          ins.Parameters.AddWithValue("$lm", lastMod);
          ins.Parameters.AddWithValue("$h", contentHash);

          var n = await ins.ExecuteNonQueryAsync(ct);
          tx.Commit();

          _log.Debug($"[SQLite] Upsert insert: ContentHash={contentHash}, rows affected={n}, Id={(metadata.Id > 0 ? metadata.Id : 0)}.");
        }
      }, ct: ct);
    }

    /// <summary>Come UpsertAsync, ma restituisce l'Id del record risultante (unico per ContentHash).</summary>
    public async Task<int> UpsertAndGetIdAsync(ProgesiMetadata metadata, CancellationToken ct = default)
    {
      if (metadata is null) throw new ArgumentNullException(nameof(metadata));

      return await WithRetryAsync(async () =>
      {
        var contentHash = ProgesiHash.Compute(metadata);

        using (var conn0 = OpenConnection())
        {
          var existed = await GetIdByHashAsync(conn0, contentHash, ct);
          if (existed > 0)
          {
            _log.Debug($"[SQLite] UpsertAndGetId: content already exists with Id={existed}.");
            return existed;
          }
        }

        await UpsertAsync(metadata, ct);

        using var conn = OpenConnection();
        var id = await GetIdByHashAsync(conn, contentHash, ct);
        if (id <= 0) throw new InvalidOperationException("UpsertAndGetIdAsync: impossibile ricavare l'Id.");
        _log.Debug($"[SQLite] UpsertAndGetId: resolved Id={id} for ContentHash={contentHash}.");
        return id;
      }, ct: ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
      {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Metadata WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        var ok = n > 0;
        _log.Debug($"[SQLite] Delete Id={id}: {(ok ? "deleted" : "not found")}.");
        return ok;
      }, ct: ct);
    }

    public async Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default)
    {
      return await WithRetryAsync(async () =>
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

        return (IReadOnlyList<ProgesiMetadata>)list;
      }, ct: ct);
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
