#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Sqlite
{
    /// <summary>
    /// Base class per repository SQLite:
    /// - OpenConnection() con PRAGMA (WAL, busy_timeout, foreign_keys)
    /// - Creazione cartella DB se mancante
    /// - Helper schema/migrazioni (ContentHash, dedup, SchemaInfo)
    /// - Retry helpers per gestire SQLITE_BUSY/SQLITE_LOCKED (con logging)
    /// - Value helpers: TypeOf / Stringify / ParseValue
    /// </summary>
    public abstract class SqliteRepositoryBase
    {
        protected readonly string _dbPath;
        protected readonly bool _resetSchema;
        protected readonly IProgesiLogger _log;

        protected SqliteRepositoryBase(string dbPath, bool resetSchema = false)
            : this(dbPath, resetSchema, NullLogger.Instance) { }

        /// <summary>Overload che permette di iniettare un logger.</summary>
        protected SqliteRepositoryBase(string dbPath, bool resetSchema, IProgesiLogger? logger)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _resetSchema = resetSchema;
            _log = logger ?? NullLogger.Instance;
            EnsureDbDirectory();
        }

        private void EnsureDbDirectory()
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
        }

        /// <summary>
        /// Apre la connessione con PRAGMA robusti per concorrenza/affidabilità.
        /// </summary>
        protected SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
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

            _log.Debug($"[SQLite] Opened connection to '{_dbPath}' (WAL, busy_timeout=5000ms).");
            return conn;
        }

        // ---------- Retry helpers ----------
        private static bool IsBusyOrLocked(SqliteException ex)
            => ex.SqliteErrorCode == 5 /*SQLITE_BUSY*/ || ex.SqliteErrorCode == 6 /*SQLITE_LOCKED*/;

        protected async Task WithRetryAsync(Func<Task> action, int maxAttempts = 5, int initialDelayMs = 50, CancellationToken ct = default)
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
                    continue;
                }
            }
        }

        protected async Task<T> WithRetryAsync<T>(Func<Task<T>> func, int maxAttempts = 5, int initialDelayMs = 50, CancellationToken ct = default)
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
                    continue;
                }
            }
        }

        // ---------- Schema helpers ----------

        /// <summary>Aggiunge una colonna se non presente.</summary>
        protected static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
        {
            if (conn is null) throw new ArgumentNullException(nameof(conn));
            if (string.IsNullOrWhiteSpace(table)) throw new ArgumentException("Table required.", nameof(table));
            if (string.IsNullOrWhiteSpace(column)) throw new ArgumentException("Column required.", nameof(column));
            if (string.IsNullOrWhiteSpace(definition)) throw new ArgumentException("Definition required.", nameof(definition));

            using (var check = conn.CreateCommand())
            {
                check.CommandText = $"PRAGMA table_info({table});";
                using var r = check.ExecuteReader();
                while (r.Read())
                {
                    var colName = r.GetString(1);
                    if (string.Equals(colName, column, StringComparison.OrdinalIgnoreCase))
                        return; // già presente
                }
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            alter.ExecuteNonQuery();
        }

        /// <summary>Assicura la colonna ContentHash e l'indice UNIQUE.</summary>
        protected static void EnsureContentHash(SqliteConnection conn, string table)
        {
            AddColumnIfMissing(conn, table, "ContentHash", "TEXT NOT NULL");
            using var idx = conn.CreateCommand();
            idx.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{table}_ContentHash ON {table}(ContentHash);";
            idx.ExecuteNonQuery();
        }

        /// <summary>Dedup di sicurezza: mantiene l'Id minimo per ogni ContentHash. Restituisce #righe eliminate.</summary>
        protected static int DeduplicateByContentHash(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {table} WHERE Id NOT IN (SELECT MIN(Id) FROM {table} GROUP BY ContentHash);";
            var affected = cmd.ExecuteNonQuery();
            return affected;
        }

        /// <summary>SchemaInfo + dedup + indice UNIQUE(ContentHash) in modo idempotente (con logging del dedup).</summary>
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
                _log.Info($"[SQLite] Deduplicated table '{table}': removed {removed} duplicate rows by ContentHash.");

            EnsureContentHash(conn, table);
        }

        // ---------- Value helpers (null-safe) ----------

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
            if (t == null) return (object)value;
            return JsonConvert.DeserializeObject(value, t) ?? (object)value;
        }
    }
}
