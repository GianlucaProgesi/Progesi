using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;

namespace ProgesiRepositories.Sqlite
{
    public abstract class SqliteRepositoryBase
    {
        protected readonly string _dbPath;
        protected readonly string _connString;
        protected readonly bool _resetSchema;

        static SqliteRepositoryBase()
        {
            SQLitePCL.Batteries_V2.Init();
        }

        protected SqliteRepositoryBase(string dbPath, bool resetSchema = false)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is null or empty", nameof(dbPath));

            _dbPath = dbPath;
            _resetSchema = resetSchema;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_dbPath)) ?? ".");
            _connString = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
        }

        protected SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(_connString);
            conn.Open();
            return conn;
        }

        protected static void EnsureContentHash(SqliteConnection conn, string table)
        {
            using (var q = conn.CreateCommand())
            {
                q.CommandText = $"PRAGMA table_info({table});";
                using var r = q.ExecuteReader();
                var hasCol = false;
                while (r.Read())
                {
                    var name = r.GetString(1);
                    if (string.Equals(name, "ContentHash", StringComparison.OrdinalIgnoreCase))
                    {
                        hasCol = true; break;
                    }
                }
                if (!hasCol)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN ContentHash TEXT;";
                    alter.ExecuteNonQuery();
                }
            }
            using (var idx = conn.CreateCommand())
            {
                idx.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{table}_ContentHash ON {table}(ContentHash);";
                idx.ExecuteNonQuery();
            }
        }

        protected static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
        {
            using var q = conn.CreateCommand();
            q.CommandText = $"PRAGMA table_info({table});";
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            alter.ExecuteNonQuery();
        }

        // -------- Value helpers (null-safe)
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
