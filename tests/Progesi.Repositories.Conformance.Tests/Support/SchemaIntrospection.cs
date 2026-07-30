using Microsoft.Data.Sqlite;

namespace Progesi.Repositories.Conformance.Tests.Support;

internal sealed record ColumnInfo(string Name, bool NotNull);

internal sealed record IndexInfo(string Name, bool IsUnique, IReadOnlyList<string> Columns);

internal static class SchemaIntrospection
{
  public static IReadOnlyList<ColumnInfo> GetColumns(SqliteConnection conn, string table)
  {
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info({table});";

    var columns = new List<ColumnInfo>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var name = reader.GetString(1);
      var notNull = reader.GetInt32(3) == 1;
      columns.Add(new ColumnInfo(name, notNull));
    }

    return columns;
  }

  public static IndexInfo? FindUniqueContentHashIndex(SqliteConnection conn, string table)
  {
    using var listCmd = conn.CreateCommand();
    listCmd.CommandText = $"PRAGMA index_list({table});";

    using var listReader = listCmd.ExecuteReader();
    while (listReader.Read())
    {
      var indexName = listReader.GetString(1);
      var isUnique = listReader.GetInt32(2) == 1;
      if (!isUnique)
        continue;

      using var infoCmd = conn.CreateCommand();
      infoCmd.CommandText = $"PRAGMA index_info({indexName});";

      var columns = new List<string>();
      using var infoReader = infoCmd.ExecuteReader();
      while (infoReader.Read())
      {
        columns.Add(infoReader.GetString(2));
      }

      if (columns.Any(c => string.Equals(c, "ContentHash", StringComparison.OrdinalIgnoreCase)))
        return new IndexInfo(indexName, true, columns);
    }

    return null;
  }
}
