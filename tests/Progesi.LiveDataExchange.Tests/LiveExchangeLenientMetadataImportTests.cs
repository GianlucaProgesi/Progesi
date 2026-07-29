using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using FluentAssertions;
using Progesi.LiveDataExchange;
using Progesi.LiveDataExchange.Tests.Support;
using Xunit;

namespace Progesi.LiveDataExchange.Tests
{
  public sealed class LiveExchangeLenientMetadataImportTests
  {
    [Fact]
    public void Excel_lenient_import_keeps_resolved_metadata_and_warns_on_dropped_ids()
    {
      var snapshot = new LiveExchangeSnapshot
      {
        Metadata = new[]
        {
          new MetadataExportRow { Id = 1, Hash = "meta-1", By = "eng", Description = "Known metadata", LM = "2026-07-29T10:00:00Z" }
        },
        Variables = new[]
        {
          new VariableExportRow
          {
            Id = 10,
            Hash = "var-10",
            Name = "LinkedVar",
            Value = "42",
            ValC = "42",
            MetadataIds = new[] { 1, 999 }
          }
        }
      };

      var codec = new FakeGeometryCodec();
      var sink = new InMemoryImportSink();
      string path = Path.Combine(Path.GetTempPath(), "progesi-lenient-meta-excel-" + Path.GetRandomFileName() + ".xlsx");

      try
      {
        LiveExchangeExcelExporter.Export(snapshot, path, overwrite: true);

        var result = LiveExchangeExcelImporter.ImportValidated(
          path, strict: false, failOnError: false, maxErrors: 1000, mapJson: "", dryRun: false, sink, codec);

        result.Errors.Should().BeEmpty();
        sink.Variables.Should().ContainSingle();
        sink.Variables[0].MetadataIds.Should().Equal(1);
        result.Warnings.Should().Contain(w => w.Text.Contains("METAID not found: 999"));
        result.Warnings.Should().Contain(w => w.Text.Contains("partial resolve") && w.Text.Contains("kept [1]") && w.Text.Contains("999"));
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
        var log = path + ".import.log.txt";
        if (File.Exists(log)) File.Delete(log);
      }
    }

    [Fact]
    public void Sqlite_lenient_import_keeps_resolved_metadata_and_warns_on_dropped_ids()
    {
      var codec = new FakeGeometryCodec();
      var sink = new InMemoryImportSink();
      string path = Path.Combine(Path.GetTempPath(), "progesi-lenient-meta-sqlite-" + Path.GetRandomFileName() + ".db");

      try
      {
        CreatePartialMetadataDb(path);

        var result = LiveExchangeSqliteImporter.ImportValidated(path, strict: false, dryRun: false, sink, codec);

        result.Errors.Should().BeEmpty();
        sink.Variables.Should().ContainSingle();
        sink.Variables[0].MetadataIds.Should().Equal(1);
        result.Warnings.Should().Contain(w => w.Text.Contains("METAID not found: 999"));
        result.Warnings.Should().Contain(w => w.Text.Contains("partial resolve") && w.Text.Contains("kept [1]") && w.Text.Contains("999"));
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
        var log = path + ".import.log.txt";
        if (File.Exists(log)) File.Delete(log);
      }
    }

    private static void CreatePartialMetadataDb(string path)
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
  Assumption INTEGER NOT NULL DEFAULT 0,
  MetadataIdsJson TEXT
);
CREATE TABLE VariableDepends (
  VarId INTEGER NOT NULL,
  DepId INTEGER NOT NULL,
  PRIMARY KEY (VarId, DepId)
);
CREATE TABLE Refs (
  MetaId INTEGER NOT NULL,
  Ref TEXT NOT NULL,
  PRIMARY KEY (MetaId, Ref)
);";
          cmd.ExecuteNonQuery();

          cmd.CommandText = "INSERT INTO Metadata (Id,Hash,By,Description,LM) VALUES (1,'meta-1','eng','Known metadata','2026-07-29T10:00:00Z')";
          cmd.ExecuteNonQuery();

          cmd.CommandText = "INSERT INTO Variables (Id,Hash,Name,Value,ValC,MetaId,Assumption,MetadataIdsJson) VALUES (10,'var-10','LinkedVar','42','42',NULL,0,'[1,999]')";
          cmd.ExecuteNonQuery();
        }
      }
    }
  }
}
