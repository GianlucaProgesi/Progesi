using System.IO;
using FluentAssertions;
using Progesi.LiveDataExchange;
using Progesi.LiveDataExchange.Tests.Support;
using Xunit;

namespace Progesi.LiveDataExchange.Tests
{
  public sealed class LiveExchangeExcelRoundTripTests
  {
    [Fact]
    public void Export_then_import_preserves_canonical_rows()
    {
      var snapshot = RoundTripFixtures.CreateCanonicalSnapshot();
      var codec = new FakeGeometryCodec();
      var sink = new InMemoryImportSink();

      foreach (var m in snapshot.Metadata)
        sink.KnownMetadataIds.Add(m.Id);

      string path = Path.Combine(Path.GetTempPath(), "progesi-live-excel-" + Path.GetRandomFileName() + ".xlsx");
      try
      {
        var (outPath, info) = LiveExchangeExcelExporter.Export(snapshot, path, overwrite: true);
        outPath.Should().Be(path);
        info.Should().Contain("OK ExportExcel");

        var result = LiveExchangeExcelImporter.ImportValidated(
          path, strict: false, failOnError: false, maxErrors: 1000, mapJson: "", dryRun: false, sink, codec);

        result.Errors.Should().BeEmpty();
        sink.Metadata.Should().HaveCount(2);
        sink.Variables.Should().HaveCount(3);
        sink.Clusters.Should().HaveCount(1);
        sink.Variables.Should().Contain(v => v.Name == "BeamCurve" && !string.IsNullOrEmpty(v.GeometryJson));
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
