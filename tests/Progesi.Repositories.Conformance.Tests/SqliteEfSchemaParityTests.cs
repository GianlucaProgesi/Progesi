using FluentAssertions;
using Microsoft.Data.Sqlite;
using Progesi.Infrastructure.EF;
using Progesi.Repositories.Conformance.Tests.Support;
using ProgesiRepositories.Sqlite;

namespace Progesi.Repositories.Conformance.Tests;

/// <summary>
/// Pins the canonical relational schema: direct SQLite (GH tier) and EF Migrate (web tier) must agree
/// on column sets, ContentHash NOT NULL, and UNIQUE(ContentHash) per core table.
/// </summary>
public sealed class SqliteEfSchemaParityTests : IDisposable
{
  private readonly string _sqlitePath;
  private readonly string _efPath;

  public SqliteEfSchemaParityTests()
  {
    _sqlitePath = Path.Combine(Path.GetTempPath(), $"progesi_schema_sqlite_{Guid.NewGuid():N}.sqlite");
    _efPath = Path.Combine(Path.GetTempPath(), $"progesi_schema_ef_{Guid.NewGuid():N}.sqlite");

    _ = new SqliteVariableRepository(_sqlitePath, resetSchema: true);
    _ = new SqliteMetadataRepository(_sqlitePath, resetSchema: false);
    _ = new SqliteClusterRepository(_sqlitePath, resetSchema: false);
    _ = new SqliteAxisVariableRepository(_sqlitePath, resetSchema: false);

    using var efContext = ProgesiDbContextFactory.Create($"Data Source={_efPath}", resetSchema: true);
  }

  [Theory]
  [InlineData("Variables")]
  [InlineData("Metadata")]
  [InlineData("Clusters")]
  [InlineData("Axis")]
  public void Sqlite_And_Ef_Schemas_Agree_On_Columns_ContentHash_NotNull_And_Unique_Index(string table)
  {
    var sqliteColumns = ReadColumns(_sqlitePath, table);
    var efColumns = ReadColumns(_efPath, table);

    sqliteColumns.Select(c => c.Name).Should().BeEquivalentTo(efColumns.Select(c => c.Name),
      because: $"{table} column sets must match between SQLite and EF");

    sqliteColumns.Single(c => c.Name == "ContentHash").NotNull.Should().BeTrue(
      because: $"SQLite {table}.ContentHash must be NOT NULL");
    efColumns.Single(c => c.Name == "ContentHash").NotNull.Should().BeTrue(
      because: $"EF {table}.ContentHash must be NOT NULL");

    var sqliteIndex = ReadUniqueContentHashIndex(_sqlitePath, table);
    var efIndex = ReadUniqueContentHashIndex(_efPath, table);

    sqliteIndex.Should().NotBeNull(because: $"SQLite {table} must have a UNIQUE index on ContentHash");
    efIndex.Should().NotBeNull(because: $"EF {table} must have a UNIQUE index on ContentHash");
    sqliteIndex!.Columns.Should().Equal("ContentHash");
    efIndex!.Columns.Should().Equal("ContentHash");
  }

  private static IReadOnlyList<ColumnInfo> ReadColumns(string dbPath, string table)
  {
    using var conn = OpenReadOnly(dbPath);
    return SchemaIntrospection.GetColumns(conn, table);
  }

  private static IndexInfo? ReadUniqueContentHashIndex(string dbPath, string table)
  {
    using var conn = OpenReadOnly(dbPath);
    return SchemaIntrospection.FindUniqueContentHashIndex(conn, table);
  }

  private static SqliteConnection OpenReadOnly(string dbPath)
  {
    var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();
    return conn;
  }

  public void Dispose()
  {
    TryDelete(_sqlitePath);
    TryDelete(_efPath);
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}
