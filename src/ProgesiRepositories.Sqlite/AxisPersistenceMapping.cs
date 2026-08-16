#nullable enable
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProgesiCore;
using ProgesiCore.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgesiRepositories.Sqlite
{
  internal static class AxisPersistenceMapping
  {
    internal sealed class StationJsonEntry
    {
      public double Position { get; set; }
      public int VariableId { get; set; }
      public ProgesiAxisStationSide Side { get; set; } = ProgesiAxisStationSide.None;
    }

    internal sealed class LabelJsonEntry
    {
      public double Position { get; set; }
      public string Label { get; set; } = string.Empty;
    }

    public static ProgesiAxisVariableDto FromDomain(ProgesiAxisVariable axis)
      => ProgesiAxisVariableDto.FromDomain(axis);

    public static ProgesiAxisVariable ToDomain(ProgesiAxisVariableDto dto)
      => ProgesiAxisVariableDto.ToDomain(dto);

    public static string SerializeStations(IEnumerable<ProgesiAxisVariableDto.Entry> entries)
    {
      var payload = entries
        .Select(e => new StationJsonEntry { Position = e.Position, VariableId = e.VariableId, Side = e.Side })
        .OrderBy(e => e.Position)
        .ThenBy(e => e.Side)
        .ThenBy(e => e.VariableId)
        .ToArray();
      return JsonConvert.SerializeObject(payload);
    }

    public static List<ProgesiAxisVariableDto.Entry> DeserializeStations(string? json)
    {
      if (string.IsNullOrWhiteSpace(json))
        return new List<ProgesiAxisVariableDto.Entry>();

      var rows = JsonConvert.DeserializeObject<StationJsonEntry[]>(json) ?? Array.Empty<StationJsonEntry>();
      return rows.Select(r => new ProgesiAxisVariableDto.Entry
      {
        Position = r.Position,
        VariableId = r.VariableId,
        Side = r.Side
      }).ToList();
    }

    public static string SerializeLabels(IEnumerable<ProgesiAxisVariableDto.LabelEntry> labels)
    {
      var payload = labels
        .Where(l => !string.IsNullOrWhiteSpace(l.Label))
        .Select(l => new LabelJsonEntry { Position = l.Position, Label = l.Label })
        .OrderBy(l => l.Position)
        .ToArray();
      return JsonConvert.SerializeObject(payload);
    }

    public static List<ProgesiAxisVariableDto.LabelEntry> DeserializeLabels(string? json)
    {
      if (string.IsNullOrWhiteSpace(json))
        return new List<ProgesiAxisVariableDto.LabelEntry>();

      var rows = JsonConvert.DeserializeObject<LabelJsonEntry[]>(json) ?? Array.Empty<LabelJsonEntry>();
      return rows.Select(r => new ProgesiAxisVariableDto.LabelEntry
      {
        Position = r.Position,
        Label = r.Label
      }).ToList();
    }

    public static ProgesiAxisVariableDto ReadDto(SqliteDataReader reader)
    {
      return new ProgesiAxisVariableDto
      {
        AxisId = reader.GetInt32(0),
        AxisName = reader.GetString(1),
        Name = reader.GetString(2),
        ValueTypeKey = reader.GetString(3),
        AxisLength = reader.IsDBNull(4) ? (double?)null : reader.GetDouble(4),
        CurvePayload = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        Mode = (AxisCurveMode)reader.GetInt32(6),
        KeyPoints = JsonConvert.DeserializeObject<List<double>>(reader.IsDBNull(7) ? "[]" : reader.GetString(7)) ?? new List<double>(),
        RuleId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
        FunctionId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
        FunctionHashtag = reader.IsDBNull(10) ? null : reader.GetString(10),
        FunctionPayload = reader.IsDBNull(11) ? null : reader.GetString(11),
        ContentHash = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
        Entries = DeserializeStations(reader.IsDBNull(12) ? "[]" : reader.GetString(12)),
        Labels = DeserializeLabels(reader.IsDBNull(13) ? "[]" : reader.GetString(13))
      };
    }

    public const string SelectColumns = @"
Id, AxisName, Name, ValueTypeKey, AxisLength, CurvePayload, Mode, KeyPointsJson,
RuleId, FunctionId, FunctionHashtag, FunctionPayload, StationsJson, LabelsJson, ContentHash, Hashtag";
  }
}
