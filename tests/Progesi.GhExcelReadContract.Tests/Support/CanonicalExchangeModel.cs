using System;
using System.Collections.Generic;

namespace Progesi.GhExcelReadContract.Tests.Support
{
  /// <summary>Fixed canonical model for R2-C.0 round-trip and golden fixture tests.</summary>
  internal sealed class CanonicalExchangeModel
  {
    public IReadOnlyList<VariableRow> Variables { get; set; } = Array.Empty<VariableRow>();
    public IReadOnlyList<MetadataRow> Metadata { get; set; } = Array.Empty<MetadataRow>();
    public IReadOnlyList<ClusterRow> Clusters { get; set; } = Array.Empty<ClusterRow>();

    internal static CanonicalExchangeModel CreateFixedModel()
    {
      const string geometryType = "Rhino.Geometry.LineCurve";
      var geometryPayload = new string('G', GhExcelObjectSheet.DefaultMaxChunkLength + 456);

      return new CanonicalExchangeModel
      {
        Variables = new[]
        {
          new VariableRow
          {
            Id = 1,
            Hash = "var-hash-1",
            Name = "Span",
            Value = "12.5",
            ValC = "12.5",
            MetaIds = new[] { 1 },
            Depends = Array.Empty<int>(),
            Assumption = false
          },
          new VariableRow
          {
            Id = 2,
            Hash = "var-hash-2",
            Name = "Width",
            Value = "3",
            ValC = "3",
            MetaIds = new[] { 1, 2 },
            Depends = new[] { 1 },
            Assumption = true
          },
          new VariableRow
          {
            Id = 3,
            Hash = "var-hash-3",
            Name = "BeamCurve",
            Value = GhExcelObjectSheet.BuildObjectMarker(geometryType),
            ValC = geometryPayload,
            MetaIds = new[] { 2 },
            Depends = new[] { 1, 2 },
            Assumption = false,
            ObjectType = geometryType,
            ObjectPayloadJson = geometryPayload
          }
        },
        Metadata = new[]
        {
          new MetadataRow
          {
            Id = 1,
            Hash = "meta-hash-1",
            By = "eng",
            Description = "Primary metadata",
            Refs = new[] { "https://example.com/a", "https://example.com/b" },
            LastModified = "2026-07-27T10:00:00Z"
          },
          new MetadataRow
          {
            Id = 2,
            Hash = "meta-hash-2",
            By = "qa",
            Description = "Secondary metadata",
            Refs = new[] { "https://example.com/c" },
            LastModified = "2026-07-27T11:00:00Z"
          }
        },
        Clusters = new[]
        {
          new ClusterRow
          {
            Id = 1,
            Hash = "cluster-hash-1",
            Name = "LoadCase",
            Description = "Set A",
            VariableIds = new[] { 1, 2, 3 }
          }
        }
      };
    }
  }

  internal sealed class VariableRow
  {
    public int Id { get; set; }
    public string Hash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string ValC { get; set; } = "";
    public int[] MetaIds { get; set; } = Array.Empty<int>();
    public int[] Depends { get; set; } = Array.Empty<int>();
    public bool Assumption { get; set; }
    public string ObjectType { get; set; } = "";
    public string ObjectPayloadJson { get; set; } = "";
  }

  internal sealed class MetadataRow
  {
    public int Id { get; set; }
    public string Hash { get; set; } = "";
    public string By { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Refs { get; set; } = Array.Empty<string>();
    public string LastModified { get; set; } = "";
  }

  internal sealed class ClusterRow
  {
    public int Id { get; set; }
    public string Hash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int[] VariableIds { get; set; } = Array.Empty<int>();
  }
}
