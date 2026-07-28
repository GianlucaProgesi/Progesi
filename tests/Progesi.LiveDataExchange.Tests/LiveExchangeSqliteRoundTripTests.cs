using System.Data.SQLite;
using System.IO;
using System.Linq;
using FluentAssertions;
using Progesi.GhExcelReadContract;
using Progesi.LiveDataExchange;
using Progesi.LiveDataExchange.Tests.Support;
using Xunit;

namespace Progesi.LiveDataExchange.Tests
{
  public sealed class LiveExchangeSqliteRoundTripTests
  {
    [Fact]
    public void Export_then_import_preserves_canonical_rows()
    {
      var snapshot = RoundTripFixtures.CreateCanonicalSnapshot();
      var codec = new FakeGeometryCodec();
      var sink = new InMemoryImportSink();

      foreach (var m in snapshot.Metadata)
        sink.KnownMetadataIds.Add(m.Id);

      string path = Path.Combine(Path.GetTempPath(), "progesi-live-sqlite-" + Path.GetRandomFileName() + ".db");
      try
      {
        var (outPath, info) = LiveExchangeSqliteExporter.Export(snapshot, path, overwrite: true);
        outPath.Should().Be(path);
        info.Should().Contain("OK ExportSqlite");

        var result = LiveExchangeSqliteImporter.ImportValidated(path, strict: false, dryRun: false, sink, codec);

        result.Info.Should().Contain("ImportSqlite");
        sink.Metadata.Should().HaveCount(2);
        sink.Variables.Should().HaveCount(3);
        sink.Clusters.Should().HaveCount(1);
        sink.Clusters[0].VariableIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        sink.Variables.Single(v => v.Name == "Span").Value.Should().Be("12.5");
        sink.Variables.Should().Contain(v => v.Name == "BeamCurve" && !string.IsNullOrEmpty(v.GeometryJson));
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
        var log = path + ".import.log.txt";
        if (File.Exists(log)) File.Delete(log);
      }
    }

    [Fact]
    public void Import_legacy_db_without_object_payload_columns_succeeds_without_geometry()
    {
      var codec = new FakeGeometryCodec();
      var sink = new InMemoryImportSink();
      string path = Path.Combine(Path.GetTempPath(), "progesi-live-sqlite-legacy-" + Path.GetRandomFileName() + ".db");

      try
      {
        CreateLegacySchemaDb(path);

        var result = LiveExchangeSqliteImporter.ImportValidated(path, strict: false, dryRun: false, sink, codec);

        result.Errors.Should().BeEmpty();
        sink.Variables.Should().ContainSingle(v => v.Name == "Span");
        sink.Variables.Single(v => v.Name == "Span").Value.Should().Be("12.5");
        sink.Variables.Single(v => v.Name == "Span").GeometryJson.Should().BeEmpty();
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
        var log = path + ".import.log.txt";
        if (File.Exists(log)) File.Delete(log);
      }
    }

    private static void CreateLegacySchemaDb(string path)
    {
      if (File.Exists(path))
        File.Delete(path);

      using (var cn = new SQLiteConnection($@"Data Source={path};Version=3;"))
      {
        cn.Open();
        using (var cmd = new SQLiteCommand(cn))
        {
          cmd.CommandText = @"
CREATE TABLE Metadata (
  Id INTEGER PRIMARY KEY,
  Hash TEXT NOT NULL,
  By TEXT,
  Description TEXT,
  LM TEXT
);
CREATE TABLE Variables (
  Id INTEGER PRIMARY KEY,
  Hash TEXT NOT NULL,
  Name TEXT NOT NULL,
  Value TEXT,
  ValC TEXT,
  MetaId INTEGER NULL,
  Assumption INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE VariableDepends (
  VarId INTEGER NOT NULL,
  DepId INTEGER NOT NULL,
  PRIMARY KEY (VarId, DepId)
);";
          cmd.ExecuteNonQuery();

          cmd.CommandText = "INSERT INTO Variables (Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (1,'h1','Span','12.5','12.5',NULL,0)";
          cmd.ExecuteNonQuery();

          string marker = GhExcelObjectSheet.BuildObjectMarker(FakeGeometryCodec.GeometryType);
          cmd.CommandText = "INSERT INTO Variables (Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (3,'h3','BeamCurve',@marker,@valc,NULL,0)";
          cmd.Parameters.Clear();
          cmd.Parameters.AddWithValue("@marker", marker);
          cmd.Parameters.AddWithValue("@valc", "payload");
          cmd.ExecuteNonQuery();
        }
      }
    }
  }
}
