using System;
using System.Collections.Generic;
using System.Linq;

namespace Progesi.GhExcelReadContract
{
  /// <summary>
  /// Pure chunk/dechunk helpers for ProgesiVariableObjects Excel sheet payloads.
  /// </summary>
  public static class GhExcelObjectSheet
  {
    public const string SheetName = "ProgesiVariableObjects";
    public const string SheetAlias = "VariableObjects";
    public const string ObjectMarkerPrefix = "@OBJECT:";
    public const int DefaultMaxChunkLength = 30000;

    public sealed class ObjectChunkRow
    {
      public int VarId { get; set; }
      public int ChunkIndex { get; set; }
      public int ChunkCount { get; set; }
      public string ObjectType { get; set; } = "";
      public string Payload { get; set; } = "";
    }

    public static string BuildObjectMarker(string objectType)
    {
      var type = (objectType ?? string.Empty).Trim();
      if (type.Length == 0)
        throw new ArgumentException("objectType is required", nameof(objectType));
      return ObjectMarkerPrefix + type;
    }

    public static bool TryParseObjectMarker(string? cellValue, out string objectType)
    {
      objectType = string.Empty;
      if (string.IsNullOrWhiteSpace(cellValue))
        return false;

      var value = cellValue.Trim();
      if (!value.StartsWith(ObjectMarkerPrefix, StringComparison.OrdinalIgnoreCase))
        return false;

      objectType = value.Substring(ObjectMarkerPrefix.Length).Trim();
      return objectType.Length > 0;
    }

    public static IReadOnlyList<ObjectChunkRow> ChunkPayload(
      int varId,
      string objectType,
      string payload,
      int maxChunkLength = DefaultMaxChunkLength)
    {
      if (varId <= 0) throw new ArgumentOutOfRangeException(nameof(varId));
      if (string.IsNullOrEmpty(objectType)) throw new ArgumentException("objectType is required", nameof(objectType));
      if (payload == null) throw new ArgumentNullException(nameof(payload));
      if (maxChunkLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkLength));

      if (payload.Length == 0)
      {
        return new[]
        {
          new ObjectChunkRow
          {
            VarId = varId,
            ChunkIndex = 0,
            ChunkCount = 1,
            ObjectType = objectType,
            Payload = string.Empty
          }
        };
      }

      var rows = new List<ObjectChunkRow>();
      int chunkCount = (payload.Length + maxChunkLength - 1) / maxChunkLength;
      for (int i = 0; i < chunkCount; i++)
      {
        int start = i * maxChunkLength;
        int length = Math.Min(maxChunkLength, payload.Length - start);
        rows.Add(new ObjectChunkRow
        {
          VarId = varId,
          ChunkIndex = i,
          ChunkCount = chunkCount,
          ObjectType = objectType,
          Payload = payload.Substring(start, length)
        });
      }

      return rows;
    }

    public static bool TryReassemblePayload(
      IEnumerable<ObjectChunkRow> rows,
      int varId,
      out string payload,
      out string objectType,
      out string error)
    {
      payload = string.Empty;
      objectType = string.Empty;
      error = string.Empty;

      if (rows == null)
      {
        error = "rows is null";
        return false;
      }

      var chunkRows = rows
        .Where(r => r != null && r.VarId == varId)
        .OrderBy(r => r.ChunkIndex)
        .ToList();

      if (chunkRows.Count == 0)
      {
        error = $"no object chunks for VarId={varId}";
        return false;
      }

      objectType = chunkRows[0].ObjectType ?? string.Empty;
      if (string.IsNullOrWhiteSpace(objectType))
      {
        error = "missing ObjectType";
        return false;
      }

      int expectedCount = chunkRows[0].ChunkCount;
      if (expectedCount <= 0 || chunkRows.Count != expectedCount)
      {
        error = $"chunk count mismatch for VarId={varId} (expected {expectedCount}, got {chunkRows.Count})";
        return false;
      }

      for (int i = 0; i < expectedCount; i++)
      {
        var row = chunkRows[i];
        if (row.ChunkIndex != i)
        {
          error = $"missing chunk index {i} for VarId={varId}";
          return false;
        }

        if (!string.Equals(row.ObjectType, objectType, StringComparison.Ordinal))
        {
          error = $"ObjectType mismatch at chunk {i} for VarId={varId}";
          return false;
        }

        if (row.ChunkCount != expectedCount)
        {
          error = $"ChunkCount mismatch at chunk {i} for VarId={varId}";
          return false;
        }
      }

      payload = string.Concat(chunkRows.Select(r => r.Payload ?? string.Empty));
      return true;
    }
  }
}
