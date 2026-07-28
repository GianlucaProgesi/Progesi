using System;
using Progesi.GhExcelReadContract;
using Progesi.LiveDataExchange;

namespace Progesi.LiveDataExchange.Tests.Support
{
  internal static class RoundTripFixtures
  {
    public static LiveExchangeSnapshot CreateCanonicalSnapshot()
    {
      const string geometryType = FakeGeometryCodec.GeometryType;
      var geometryPayload = new string('G', GhExcelObjectSheet.DefaultMaxChunkLength + 456);

      return new LiveExchangeSnapshot
      {
        Metadata = new[]
        {
          new MetadataExportRow { Id = 1, Hash = "meta-hash-1", By = "eng", Description = "Primary metadata", Refs = new[] { "https://example.com/a", "https://example.com/b" }, LM = "2026-07-27T10:00:00Z" },
          new MetadataExportRow { Id = 2, Hash = "meta-hash-2", By = "qa", Description = "Secondary metadata", Refs = new[] { "https://example.com/c" }, LM = "2026-07-27T11:00:00Z" }
        },
        Variables = new[]
        {
          new VariableExportRow { Id = 1, Hash = "var-hash-1", Name = "Span", Value = "12.5", ValC = "12.5", MetadataIds = new[] { 1 }, Depends = Array.Empty<int>(), Assumption = false },
          new VariableExportRow { Id = 2, Hash = "var-hash-2", Name = "Width", Value = "3", ValC = "3", MetaId = 1, MetadataIds = new[] { 1, 2 }, Depends = new[] { 1 }, Assumption = true },
          new VariableExportRow
          {
            Id = 3,
            Hash = "var-hash-3",
            Name = "BeamCurve",
            Value = GhExcelObjectSheet.BuildObjectMarker(geometryType),
            ValC = geometryPayload,
            MetadataIds = new[] { 2 },
            Depends = new[] { 1, 2 },
            Assumption = false,
            ObjectType = geometryType,
            ObjectPayloadJson = geometryPayload
          }
        },
        Clusters = new[]
        {
          new ClusterExportRow { Id = 1, Hash = "cluster-hash-1", Name = "LoadCase", Description = "Set A", VariableIds = new[] { 1, 2, 3 } }
        }
      };
    }
  }
}
