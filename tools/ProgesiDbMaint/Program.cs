#nullable enable
using Microsoft.Data.Sqlite;
using SQLitePCL; // per Batteries_V2.Init()
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
        Console.WriteLine("  init <dbPath>             # crea file DB e schema standard (Metadata/Variables)");
        Console.WriteLine("  seed-demo <dbPath>        # inserisce dati di esempio (idempotente)");
        Console.WriteLine("  stats <dbPath>");
        Console.WriteLine("  integrity <dbPath>");
        Console.WriteLine("  vacuum <dbPath>");
        Console.WriteLine("  checkpoint <dbPath>");
        Console.WriteLine("  dedup <dbPath> <table>");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(@"  init C:\data\progesi.sqlite");
        Console.WriteLine(@"  seed-demo C:\data\progesi.sqlite");
        Console.WriteLine(@"  stats C:\data\progesi.sqlite");
        Console.WriteLine(@"  dedup C:\data\progesi.sqlite Metadata");
        Console.WriteLine(@"  dedup C:\data\progesi.sqlite Variables");
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
        // calcolo hash deterministico per dedup
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

        // due metadata "demo" con snip minimo (content base64 "Hello Progesi!")
        var json1 =
            "{\"CreatedBy\":\"seed\",\"AdditionalInfo\":\"demo A\",\"References\":[\"https://example.com/A\"],\"Snips\":[{\"Id\":\"" +
            Guid.NewGuid().ToString() + "\",\"Caption\":\"cap A\",\"MimeType\":\"text/plain\",\"Content\":\"SGVsbG8gUHJvZ2VzaSE=\",\"Source\":null}]}";

        var json2 =
            "{\"CreatedBy\":\"seed\",\"AdditionalInfo\":\"demo B\",\"References\":[\"https://example.com/B\",\"https://example.com/C\"],\"Snips\":[{\"Id\":\"" +
            Guid.NewGuid().ToString() + "\",\"Caption\":\"cap B\",\"MimeType\":\"text/plain\",\"Content\":\"VGVzdCBkZW1v\",\"Source\":null}]}";

        var m1 = UpsertMetadata(c, json1, lm);
        var m2 = UpsertMetadata(c, json2, lm);

        // tre variables
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
}
