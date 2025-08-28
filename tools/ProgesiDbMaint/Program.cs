#nullable enable
using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.IO;

static class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

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
                case "stats":       Stats(db); break;
                case "integrity":   Integrity(db); break;
                case "vacuum":      Vacuum(db); break;
                case "checkpoint":  Checkpoint(db); break;
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
        Console.WriteLine("  stats <dbPath>");
        Console.WriteLine("  integrity <dbPath>");
        Console.WriteLine("  vacuum <dbPath>");
        Console.WriteLine("  checkpoint <dbPath>");
        Console.WriteLine("  dedup <dbPath> <table>");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(@"  stats C:\data\progesi.sqlite");
        Console.WriteLine(@"  dedup C:\data\progesi.sqlite Metadata");
        Console.WriteLine(@"  dedup C:\data\progesi.sqlite Variables");
    }

    static SqliteConnection Open(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("DB not found", path);
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

    static long FileSize(string p) => new FileInfo(p).Length;

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
