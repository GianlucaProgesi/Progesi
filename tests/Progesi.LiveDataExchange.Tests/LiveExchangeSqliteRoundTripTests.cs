using System.IO;
using FluentAssertions;
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
      var sink = new InMemoryImportSink();

      foreach (var m in snapshot.Metadata)
        sink.KnownMetadataIds.Add(m.Id);

      string path = Path.Combine(Path.GetTempPath(), "progesi-live-sqlite-" + Path.GetRandomFileName() + ".db");
      try
      {
        var (outPath, info) = LiveExchangeSqliteExporter.Export(snapshot, path, overwrite: true);
        outPath.Should().Be(path);
        info.Should().Contain("OK ExportSqlite");

        var result = LiveExchangeSqliteImporter.ImportValidated(path, strict: false, dryRun: false, sink);

        result.Info.Should().Contain("ImportSqlite");
        sink.Metadata.Should().HaveCount(2);
        sink.Variables.Should().HaveCount(3);
        sink.Clusters.Should().HaveCount(1);
        sink.Clusters[0].VariableIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
      }
      finally
      {
        if (File.Exists(path)) File.Delete(path);
        var log = path + ".import.log.txt";
        if (File.Exists(log)) File.Delete(log);
      }
    }
  }
}
