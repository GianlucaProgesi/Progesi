using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore.Serialization
{
  /// <summary>
  /// DTO piatto per serializzare/deserializzare ProgesiAxisVariable (single-series).
  ///
  /// Contratto importante:
  /// - Position è SEMPRE normalizzata nel dominio [0,1].
  /// - Name e ValueTypeKey sono univoci per l'intero oggetto.
  ///
  /// Tabelle suggerite:
  ///   Axis:      (AxisId, AxisName, AxisLength, Name, ValueTypeKey, RuleId, CurvePayload, Mode, KeyPointsJson, FunctionRef...)
  ///   AxisEntry: (AxisId, Position, VariableId)
  /// </summary>
  public sealed class ProgesiAxisVariableDto
  {
    public int AxisId { get; set; }
    public string AxisName { get; set; } = string.Empty;
    public double? AxisLength { get; set; }

    public string CurvePayload { get; set; } = string.Empty;
    public AxisCurveMode Mode { get; set; } = AxisCurveMode.Curve3d;
    public List<double> KeyPoints { get; set; } = new List<double>();

    public string Name { get; set; } = string.Empty;
    public string ValueTypeKey { get; set; } = string.Empty;

    public int? RuleId { get; set; }

    public int? FunctionId { get; set; }
    public string? FunctionHashtag { get; set; }
    public string? FunctionPayload { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public List<Entry> Entries { get; set; } = new List<Entry>();

    public sealed class Entry
    {
      /// <summary>Posizione normalizzata in [0,1].</summary>
      public double Position { get; set; }
      public int VariableId { get; set; }
    }

    public static ProgesiAxisVariableDto FromDomain(ProgesiAxisVariable axis)
    {
      Guard.Against.Null(axis, nameof(axis));

      var dto = new ProgesiAxisVariableDto
      {
        AxisId = axis.Id,
        AxisName = axis.AxisName,
        AxisLength = axis.AxisLength,
        CurvePayload = axis.CurvePayload,
        Mode = axis.Mode,
        KeyPoints = axis.KeyPoints.ToList(),
        Name = axis.Name,
        ValueTypeKey = axis.ValueTypeKey,
        RuleId = axis.RuleId,
        ContentHash = axis.ContentHash,
        Entries = new List<Entry>()
      };

      if (!axis.FunctionRef.IsEmpty)
      {
        dto.FunctionId = axis.FunctionRef.FunctionId;
        dto.FunctionHashtag = axis.FunctionRef.FunctionHashtag;
        if (axis.FunctionRef.Embedded != null)
          dto.FunctionPayload = axis.FunctionRef.Embedded.ToJson();
      }

      foreach (var t in axis.EnumerateAll())
      {
        dto.Entries.Add(new Entry
        {
          Position = t.positionNormalized,
          VariableId = t.variableId
        });
      }

      return dto;
    }

    public static ProgesiAxisVariable ToDomain(ProgesiAxisVariableDto dto, double tol = ProgesiAxisVariable.DefaultTolerance)
    {
      Guard.Against.Null(dto, nameof(dto));
      Guard.Against.Negative(dto.AxisId, nameof(dto.AxisId));
      Guard.Against.NullOrWhiteSpace(dto.AxisName, nameof(dto.AxisName));
      Guard.Against.NullOrWhiteSpace(dto.Name, nameof(dto.Name));
      Guard.Against.NullOrWhiteSpace(dto.ValueTypeKey, nameof(dto.ValueTypeKey));
      if (dto.AxisLength.HasValue) Guard.Against.NegativeOrZero(dto.AxisLength.Value, nameof(dto.AxisLength));
      if (dto.RuleId.HasValue) Guard.Against.Negative(dto.RuleId.Value, nameof(dto.RuleId));
      if (dto.FunctionId.HasValue) Guard.Against.Negative(dto.FunctionId.Value, nameof(dto.FunctionId));

      var functionRef = BuildFunctionRef(dto);
      var axis = new ProgesiAxisVariable(
        dto.AxisId,
        dto.AxisName,
        dto.Name,
        dto.ValueTypeKey,
        dto.AxisLength,
        dto.RuleId,
        dto.CurvePayload,
        dto.Mode,
        dto.KeyPoints,
        functionRef);

      if (dto.Entries != null)
      {
        foreach (var e in dto.Entries)
        {
          Guard.Against.Null(e, nameof(dto.Entries));
          if (double.IsNaN(e.Position) || double.IsInfinity(e.Position))
            throw new ArgumentOutOfRangeException(nameof(e.Position), "Position must be finite.");
          Guard.Against.Negative(e.VariableId, nameof(e.VariableId));
          axis.AddUnsafe(e.Position, e.VariableId, tol);
        }
      }

      return axis;
    }

    private static ProgesiFunctionRef BuildFunctionRef(ProgesiAxisVariableDto dto)
    {
      if (!string.IsNullOrWhiteSpace(dto.FunctionPayload))
        return ProgesiFunctionRef.Embed(ProgesiFunction.FromJson(dto.FunctionPayload));

      if (dto.FunctionId.HasValue)
        return ProgesiFunctionRef.ById(dto.FunctionId.Value);

      if (!string.IsNullOrWhiteSpace(dto.FunctionHashtag))
        return ProgesiFunctionRef.ByHashtag(dto.FunctionHashtag);

      return ProgesiFunctionRef.Empty;
    }

    public IEnumerable<(int AxisId, double Position, int VariableId)> EnumerateFlat()
    {
      foreach (var e in Entries)
        yield return (AxisId, e.Position, e.VariableId);
    }
  }
}
