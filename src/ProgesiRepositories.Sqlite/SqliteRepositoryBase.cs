#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
  /// <summary>
  /// Base class per repository SQLite:
  /// - Supporta path su file O connection string (file:, Data Source=, mode=memory, ecc.)
  /// - OpenConnection() con PRAGMA (WAL, busy_timeout, foreign_keys)
  /// - Helper schema/migrazioni (ContentHash, dedup, SchemaInfo)
  /// - Retry helpers per gestire SQLITE_BUSY/SQLITE_LOCKED
  /// - Value helpers
  /// </summary>
  public abstract class SqliteRepositoryBase
  {
    protected readonly string _connectionString;
    protected readonly bool _resetSchema;
    protected readonly IProgesiLogger _log;

    protected SqliteRepositoryBase(string dbPathOrConnectionString, bool resetSchema = false)
      : this(dbPathOrConnectionString, resetSchema, NullLogger.Instance) { }

    protected SqliteRepositoryBase(string dbPathOrConnectionString, bool resetSchema, IProgesiLogger? logger)
    {
      if (string.IsNullOrWhiteSpace(dbPathOrConnectionString))
        throw new ArgumentNullException(nameof(dbPathOrConnectionString));

      _resetSchema = resetSchema;
      _log = logger ?? NullLogger.Instance;

      _connectionString = BuildConnectionString(dbPathOrConnectionString);
      EnsureDbDirectoryIfNeeded(dbPathOrConnectionString);
    }

    // ------------------------------------------------------------
    // Connection string handling
    // ------------------------------------------------------------

    private static string BuildConnectionString(string input)
    {
      // Se sembra già una connection string o URI SQLite → usala così com'è
      if (LooksLikeConnectionString(input))
        return input;

      // Altrimenti trattalo come path su file
      return $"Data Source={input};Cache=Shared";
    }

    private static bool LooksLikeConnectionString(string s)
    {
      var x = s.Trim();

      if (x.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        return true;

      // .NET Framework 4.8: string.Contains non ha overload con StringComparison
      if (x.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      if (x.IndexOf("Mode=", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      if (x.IndexOf("Cache=", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      return false;
    }


    private static void EnsureDbDirectoryIfNeeded(string input)
    {
      // Solo se è un path su file (non URI / connection string)
      if (LooksLikeConnectionString(input))
        return;

      var dir = Path.GetDirectoryName(input);
      if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
    }

    // ------------------------------------------------------------
    // Connection
    // ------------------------------------------------------------

    protected SqliteConnection OpenConnection()
    {
      var conn = new SqliteConnection(_connectionString);
      conn.Open();

      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
      }

      _log.Debug($"[SQLite] Opened connection: {_connectionString}");
      return conn;
    }

    // ------------------------------------------------------------
    // Retry helpers
    // ------------------------------------------------------------

    private static bool IsBusyOrLocked(SqliteException ex)
      => ex.SqliteErrorCode == 5 /*SQLITE_BUSY*/ || ex.SqliteErrorCode == 6 /*SQLITE_LOCKED*/;

    protected async Task WithRetryAsync(
      Func<Task> action,
      int maxAttempts = 5,
      int initialDelayMs = 50,
      CancellationToken ct = default)
    {
      var delay = initialDelayMs;
      for (int attempt = 1; ; attempt++)
      {
        try
        {
          await action().ConfigureAwait(false);
          return;
        }
        catch (SqliteException ex) when (IsBusyOrLocked(ex) && attempt < maxAttempts)
        {
          _log.Warn($"[SQLite] Busy/Locked (attempt {attempt}/{maxAttempts}), retry in {delay}ms...");
          await Task.Delay(delay, ct).ConfigureAwait(false);
          delay = Math.Min(delay * 2, 1000);
        }
      }
    }

    protected async Task<T> WithRetryAsync<T>(
      Func<Task<T>> func,
      int maxAttempts = 5,
      int initialDelayMs = 50,
      CancellationToken ct = default)
    {
      var delay = initialDelayMs;
      for (int attempt = 1; ; attempt++)
      {
        try
        {
          return await func().ConfigureAwait(false);
        }
        catch (SqliteException ex) when (IsBusyOrLocked(ex) && attempt < maxAttempts)
        {
          _log.Warn($"[SQLite] Busy/Locked (attempt {attempt}/{maxAttempts}), retry in {delay}ms...");
          await Task.Delay(delay, ct).ConfigureAwait(false);
          delay = Math.Min(delay * 2, 1000);
        }
      }
    }

    // ------------------------------------------------------------
    // Schema helpers
    // ------------------------------------------------------------

    protected static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
    {
      using (var check = conn.CreateCommand())
      {
        check.CommandText = $"PRAGMA table_info({table});";
        using var r = check.ExecuteReader();
        while (r.Read())
        {
          var colName = r.GetString(1);
          if (string.Equals(colName, column, StringComparison.OrdinalIgnoreCase))
            return;
        }
      }

      using var alter = conn.CreateCommand();
      alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
      alter.ExecuteNonQuery();
    }

    protected static void EnsureContentHash(SqliteConnection conn, string table)
    {
      AddColumnIfMissing(conn, table, "ContentHash", "TEXT NOT NULL");
      using var idx = conn.CreateCommand();
      idx.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{table}_ContentHash ON {table}(ContentHash);";
      idx.ExecuteNonQuery();
    }

    protected static int DeduplicateByContentHash(SqliteConnection conn, string table)
    {
      using var cmd = conn.CreateCommand();
      cmd.CommandText = $"DELETE FROM {table} WHERE Id NOT IN (SELECT MIN(Id) FROM {table} GROUP BY ContentHash);";
      return cmd.ExecuteNonQuery();
    }

    protected void EnsureSchemaInfoAndCleanup(SqliteConnection conn, string table)
    {
      using (var cmd = conn.CreateCommand())
      {
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS __SchemaInfo (Version INTEGER NOT NULL);
INSERT INTO __SchemaInfo(Version)
SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM __SchemaInfo);";
        cmd.ExecuteNonQuery();
      }

      var removed = DeduplicateByContentHash(conn, table);
      if (removed > 0)
        _log.Info($"[SQLite] Deduplicated table '{table}': removed {removed} rows.");

      EnsureContentHash(conn, table);
    }

    // ------------------------------------------------------------
    // Value helpers
    // ------------------------------------------------------------

    protected static string TypeOf(object? obj)
    {
      if (obj == null) return "null";
      return obj switch
      {
        string _ => "string",
        int _ => "int",
        double _ => "double",
        bool _ => "bool",
        _ => obj.GetType().AssemblyQualifiedName ?? "object"
      };
    }

    protected static string Stringify(object? obj)
    {
      if (obj == null) return "null";
      return obj switch
      {
        string s => s,
        int i => i.ToString(),
        double d => d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => JsonConvert.SerializeObject(obj) ?? string.Empty
      };
    }

    protected static object? ParseValue(string value, string valueType)
    {
      if (valueType == "null") return null;
      return valueType switch
      {
        "string" => value,
        "int" => int.Parse(value),
        "double" => double.Parse(value, CultureInfo.InvariantCulture),
        "bool" => value == "true",
        _ => DeserializeOrReturn(value, valueType)
      };
    }

    private static object DeserializeOrReturn(string value, string typeName)
    {
      var t = Type.GetType(typeName, throwOnError: false);
      if (t == null) return value;
      return JsonConvert.DeserializeObject(value, t) ?? value;
    }
  }
}
