#nullable enable
using Microsoft.Data.Sqlite;
using SQLitePCL; // Batteries_V2.Init()
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

static class Program
{
  static int Main(string[] args)
  {
    Console.OutputEncoding = Encoding.UTF8;
    Batteries_V2.Init(); // inizializza bundle e_sqlite3

    if (args.Length < 2)
    {
      PrintHelp();
      return 1;
    }

    var cmd = args[0].ToLowerInvariant();
    var db = args[1];

    try
    {
      switch (cmd)
      {
        case "init":
          Init(db);
          break;

        case "seed-demo":
          SeedDemo(db);
          break;

        case "stats":
          Stats(db);
          break;

        case "integrity":
          Integrity(db);
          break;

        case "vacuum":
          Vacuum(db);
          break;

        case "checkpoint":
          Checkpoint(db);
          break;

        case "dedup":
          if (args.Length < 3) { Console.WriteLine("Usage: dedup <dbPath> <table>"); return 2; }
          Dedup(db, args[2]);
          break;

        case "migrate":
          Migrate(db);
          break;

        case "backup":
          if (args.Length < 3) { Console.WriteLine("Usage: backup <dbPath> <destPath>"); return 2; }
          Backup(db, args[2]);
          break;

        case "analyze":
          Analyze(db);
          break;

        default:
          PrintHelp(); return 2;
      }
      return 0;
    }
    catch (Exception ex)
    {
      Console.WriteLine("ERROR: " + ex);
      return 3;
    }
  }

  static void PrintHelp()
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("  init <dbPath>              # crea file DB e schema (Metadata/Variables)");
    Console.WriteLine("  seed-demo <dbPath>         # inserisce dati di esempio (idempotente)");
    Console.WriteLine("  stats <dbPath>");
    Console.WriteLine("  integrity <dbPath>");
    Console.WriteLine("  vacuum <dbPath>");
    Console.WriteLine("  checkpoint <dbPath>");
    Console.WriteLine("  dedup <dbPath> <table>");
    Console.WriteLine("  migrate <dbPath>           # applica migrazioni schema (__SchemaInfo)");
    Console.WriteLine("  backup <dbPath> <destPath> # backup consistente (VACUUM INTO)");
    Console.WriteLine("  analyze <dbPath>           # ANALYZE + PRAGMA optimize");
    Console.WriteLine();
  }

  static SqliteConnection Open(string path, bool createIfMissing = false)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
      Directory.CreateDirectory(dir!);

    if (!File.Exists(path))
    {
      if (createIfMissing)
      {
        using (File.Create(path)) { }
      }
      else
      {
        throw new FileNotFoundException("DB not found", path);
      }
    }

    var conn = new SqliteConnection($"Data Source={path};Cache=Shared");
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=5000;";
    cmd.ExecuteNonQuery();
    return conn;
  }

  static bool TableExists(SqliteConnection c, string name)
  {
    using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n;";
    cmd.Parameters.AddWithValue("$n", name);
    return cmd.ExecuteScalar() != null;
  }

  static void EnsureSchema(SqliteConnection c)
  {
    using (var cmd = c.CreateCommand())
    {
      cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Metadata (
    Id           INTEGER PRIMARY KEY,
    Json         TEXT NOT NULL,
    LastModified TEXT NOT NULL,
    ContentHash  TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Variables (
    Id           INTEGER PRIMARY KEY,
    Name         TEXT NOT NULL,
    ValueType    TEXT NOT NULL,
    Value        TEXT NOT NULL,
    MetadataId   INTEGER NULL,
    DependsJson  TEXT NOT NULL,
    ContentHash  TEXT
);
CREATE TABLE IF NOT EXISTS __SchemaInfo (Version INTEGER NOT NULL);
INSERT INTO __SchemaInfo(Version)
SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM __SchemaInfo);";
      cmd.ExecuteNonQuery();
    }

    using (var idx1 = c.CreateCommand())
    {
      idx1.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Metadata_ContentHash ON Metadata(ContentHash);";
      idx1.ExecuteNonQuery();
    }
    using (var idx2 = c.CreateCommand())
    {
      idx2.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Variables_ContentHash ON Variables(ContentHash);";
      idx2.ExecuteNonQuery();
    }
  }

  static int GetSchemaVersion(SqliteConnection c)
  {
    using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT Version FROM __SchemaInfo LIMIT 1;";
    var v = cmd.ExecuteScalar();
    return v is null ? 1 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
  }

  static void SetSchemaVersion(SqliteConnection c, int v)
  {
    using var cmd = c.CreateCommand();
    cmd.CommandText = "UPDATE __SchemaInfo SET Version=$v;";
    cmd.Parameters.AddWithValue("$v", v);
    cmd.ExecuteNonQuery();
  }

  static string NowIso() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

  static string Sha256Hex(string s)
  {
    using var sha = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(s);
    var hash = sha.ComputeHash(bytes);
    var sb = new StringBuilder(hash.Length * 2);
    foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
    return sb.ToString();
  }

  static long UpsertMetadata(SqliteConnection c, string json, string lastModifiedIso)
  {
    var h = Sha256Hex(json);
    using (var ins = c.CreateCommand())
    {
      ins.CommandText = "INSERT OR IGNORE INTO Metadata(Json,LastModified,ContentHash) VALUES($j,$lm,$h);";
      ins.Parameters.AddWithValue("$j", json);
      ins.Parameters.AddWithValue("$lm", lastModifiedIso);
      ins.Parameters.AddWithValue("$h", h);
      ins.ExecuteNonQuery();
    }
    using (var sel = c.CreateCommand())
    {
      sel.CommandText = "SELECT Id FROM Metadata WHERE ContentHash=$h;";
      sel.Parameters.AddWithValue("$h", h);
      var idObj = sel.ExecuteScalar();
      return Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
    }
  }

  static long UpsertVariable(SqliteConnection c, string name, string valueType, string value, long? metadataId, string dependsJson)
  {
    var md = metadataId.HasValue ? metadataId.Value.ToString(CultureInfo.InvariantCulture) : "";
    var content = $"{name}|{valueType}|{value}|{dependsJson}|{md}";
    var h = Sha256Hex(content);

    using (var ins = c.CreateCommand())
    {
      ins.CommandText = @"
INSERT OR IGNORE INTO Variables(Name,ValueType,Value,MetadataId,DependsJson,ContentHash)
VALUES($n,$t,$v,$m,$d,$h);";
      ins.Parameters.AddWithValue("$n", name);
      ins.Parameters.AddWithValue("$t", valueType);
      ins.Parameters.AddWithValue("$v", value);
      if (metadataId.HasValue) ins.Parameters.AddWithValue("$m", metadataId.Value);
      else ins.Parameters.AddWithValue("$m", DBNull.Value);
      ins.Parameters.AddWithValue("$d", dependsJson);
      ins.Parameters.AddWithValue("$h", h);
      ins.ExecuteNonQuery();
    }
    using (var sel = c.CreateCommand())
    {
      sel.CommandText = "SELECT Id FROM Variables WHERE ContentHash=$h;";
      sel.Parameters.AddWithValue("$h", h);
      var idObj = sel.ExecuteScalar();
      return Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
    }
  }

  static long FileSize(string p) => new FileInfo(p).Length;

  // ---- Commands ----

  static void Init(string dbPath)
  {
    using var c = Open(dbPath, createIfMissing: true);
    EnsureSchema(c);
    Console.WriteLine($"Initialized DB at: {dbPath}");
    Stats(dbPath);
  }

  static void SeedDemo(string dbPath)
  {
    using var c = Open(dbPath, createIfMissing: true);
    EnsureSchema(c);

    using var tx = c.BeginTransaction();

    var lm = NowIso();

    var json1 =
        "{\"CreatedBy\":\"seed\",\"AdditionalInfo\":\"demo A\",\"References\":[\"https://example.com/A\"],\"Snips\":[{\"Id\":\"" +
        Guid.NewGuid().ToString() + "\",\"Caption\":\"cap A\",\"MimeType\":\"text/plain\",\"Content\":\"SGVsbG8gUHJvZ2VzaSE=\",\"Source\":null}]}";

    var json2 =
        "{\"CreatedBy\":\"seed\",\"AdditionalInfo\":\"demo B\",\"References\":[\"https://example.com/B\",\"https://example.com/C\"],\"Snips\":[{\"Id\":\"" +
        Guid.NewGuid().ToString() + "\",\"Caption\":\"cap B\",\"MimeType\":\"text/plain\",\"Content\":\"VGVzdCBkZW1v\",\"Source\":null}]}";

    var m1 = UpsertMetadata(c, json1, lm);
    var m2 = UpsertMetadata(c, json2, lm);

    var v1 = UpsertVariable(c, "alpha", "int", "1", m1, "[]");
    var v2 = UpsertVariable(c, "beta", "string", "hello", m1, "[ " + v1.ToString(CultureInfo.InvariantCulture) + " ]");
    var v3 = UpsertVariable(c, "gamma", "double", "3.14", m2, "[]");

    tx.Commit();

    Console.WriteLine("Seed complete:");
    Console.WriteLine($"  Metadata: inserted/kept Ids = {m1}, {m2}");
    Console.WriteLine($"  Variables: inserted/kept Ids = {v1}, {v2}, {v3}");
    Console.WriteLine();
    Stats(dbPath);
  }

  static void Stats(string dbPath)
  {
    using var c = Open(dbPath);
    Console.WriteLine($"DB: {dbPath}");
    Console.WriteLine($"Size: {FileSize(dbPath):N0} bytes");

    foreach (var t in new[] { "Metadata", "Variables", "__SchemaInfo" })
    {
      if (!TableExists(c, t)) continue;
      using var cmd = c.CreateCommand();
      cmd.CommandText = $"SELECT COUNT(*) FROM {t};";
      var n = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
      Console.WriteLine($"{t}: {n} rows");
    }
  }

  static void Integrity(string dbPath)
  {
    using var c = Open(dbPath);
    using var cmd = c.CreateCommand();
    cmd.CommandText = "PRAGMA integrity_check;";
    var res = (string)cmd.ExecuteScalar()!;
    Console.WriteLine("integrity_check: " + res);
  }

  static void Vacuum(string dbPath)
  {
    using var c = Open(dbPath);
    using var cmd = c.CreateCommand();
    cmd.CommandText = "VACUUM;";
    cmd.ExecuteNonQuery();
    Console.WriteLine("VACUUM done.");
  }

  static void Checkpoint(string dbPath)
  {
    using var c = Open(dbPath);
    using var cmd = c.CreateCommand();
    cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
    using var r = cmd.ExecuteReader();
    if (r.Read())
    {
      var rc = r.GetInt32(0);
      var log = r.GetInt32(1);
      var ckpt = r.GetInt32(2);
      Console.WriteLine($"wal_checkpoint(FULL): rc={rc} log_frames={log} checkpointed={ckpt}");
    }
  }

  static void Dedup(string dbPath, string table)
  {
    using var c = Open(dbPath);
    if (!TableExists(c, table))
    {
      Console.WriteLine($"Table '{table}' not found.");
      return;
    }

    using (var idx = c.CreateCommand())
    {
      idx.CommandText = $"CREATE INDEX IF NOT EXISTS IX_{table}_ContentHash ON {table}(ContentHash);";
      idx.ExecuteNonQuery();
    }

    using (var del = c.CreateCommand())
    {
      del.CommandText = $"DELETE FROM {table} WHERE Id NOT IN (SELECT MIN(Id) FROM {table} GROUP BY ContentHash);";
      var n = del.ExecuteNonQuery();
      Console.WriteLine($"Dedup on '{table}': removed {n} rows.");
    }
  }

  static void Migrate(string dbPath)
  {
    using var c = Open(dbPath, createIfMissing: false);
    EnsureSchema(c);

    var before = GetSchemaVersion(c);
    var v = before;

    using (var tx = c.BeginTransaction())
    {
      // v1 -> v2: indici operativi aggiuntivi
      if (v < 2)
      {
        using (var cmd = c.CreateCommand())
        {
          // index non-unique utili per query frequenti
          cmd.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_Variables_Name ON Variables(Name);
CREATE INDEX IF NOT EXISTS IX_Variables_MetadataId ON Variables(MetadataId) WHERE MetadataId IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_Metadata_LastModified ON Metadata(LastModified);";
          cmd.ExecuteNonQuery();
        }
        v = 2;
        SetSchemaVersion(c, v);
      }

      tx.Commit();
    }

    Console.WriteLine($"Migrate: {before} -> {v}");
    Stats(dbPath);
  }

  static void Backup(string dbPath, string destPath)
  {
    var destDir = Path.GetDirectoryName(destPath);
    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
      Directory.CreateDirectory(destDir!);

    using var c = Open(dbPath);
    using var cmd = c.CreateCommand();
    cmd.CommandText = "VACUUM INTO $out;";
    cmd.Parameters.AddWithValue("$out", destPath);
    cmd.ExecuteNonQuery();

    Console.WriteLine($"Backup written to: {destPath}  (size: {FileSize(destPath):N0} bytes)");
  }

  static void Analyze(string dbPath)
  {
    using var c = Open(dbPath);
    using (var cmd = c.CreateCommand())
    {
      cmd.CommandText = "ANALYZE;";
      cmd.ExecuteNonQuery();
    }
    using (var cmd = c.CreateCommand())
    {
      cmd.CommandText = "PRAGMA optimize;";
      cmd.ExecuteNonQuery();
    }
    Console.WriteLine("ANALYZE + PRAGMA optimize done.");
  }
}
