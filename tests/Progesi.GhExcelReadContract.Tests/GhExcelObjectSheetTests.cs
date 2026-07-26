using FluentAssertions;
using Progesi.GhExcelReadContract;
using System.Linq;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelObjectSheetTests
  {
    [Fact]
    public void BuildObjectMarker_And_TryParseObjectMarker_RoundTrip()
    {
      var marker = GhExcelObjectSheet.BuildObjectMarker("Rhino.Geometry.LineCurve");

      GhExcelObjectSheet.TryParseObjectMarker(marker, out var type).Should().BeTrue();
      type.Should().Be("Rhino.Geometry.LineCurve");
    }

    [Fact]
    public void ChunkPayload_SingleChunk_When_Payload_Fits()
    {
      var rows = GhExcelObjectSheet.ChunkPayload(5, "Rhino.Geometry.Point3d", "{\"x\":1}");

      rows.Should().HaveCount(1);
      rows[0].VarId.Should().Be(5);
      rows[0].ChunkIndex.Should().Be(0);
      rows[0].ChunkCount.Should().Be(1);
      rows[0].ObjectType.Should().Be("Rhino.Geometry.Point3d");
      rows[0].Payload.Should().Be("{\"x\":1}");
    }

    [Fact]
    public void ChunkPayload_And_TryReassemblePayload_RoundTrip_MultiChunk()
    {
      var payload = new string('A', GhExcelObjectSheet.DefaultMaxChunkLength + 123);
      var rows = GhExcelObjectSheet.ChunkPayload(9, "Rhino.Geometry.PolylineCurve", payload);

      rows.Should().HaveCount(2);
      rows[0].ChunkCount.Should().Be(2);
      rows[1].ChunkIndex.Should().Be(1);

      GhExcelObjectSheet.TryReassemblePayload(rows, 9, out var rebuilt, out var type, out var error)
        .Should().BeTrue(error);

      rebuilt.Should().Be(payload);
      type.Should().Be("Rhino.Geometry.PolylineCurve");
    }

    [Fact]
    public void TryReassemblePayload_Fails_When_Chunk_Missing()
    {
      var payload = new string('B', GhExcelObjectSheet.DefaultMaxChunkLength + 50);
      var rows = GhExcelObjectSheet.ChunkPayload(3, "Rhino.Geometry.Curve", payload).ToList();
      rows.RemoveAt(1);

      GhExcelObjectSheet.TryReassemblePayload(rows, 3, out _, out _, out var error)
        .Should().BeFalse();

      error.Should().Contain("chunk count mismatch");
    }

    [Fact]
    public void TryReassemblePayload_Fails_When_VarId_Not_Found()
    {
      var rows = GhExcelObjectSheet.ChunkPayload(4, "Rhino.Geometry.Curve", "abc");

      GhExcelObjectSheet.TryReassemblePayload(rows, 99, out _, out _, out var error)
        .Should().BeFalse();

      error.Should().Contain("no object chunks");
    }
  }
}
