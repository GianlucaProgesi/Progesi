using System;
using System.Collections.Generic;
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
  ///   Axis:      (AxisId, AxisName, AxisLength, Name, ValueTypeKey, RuleId)
  ///   AxisEntry: (AxisId, Position, VariableId)
  /// </summary>
  public sealed class ProgesiAxisVariableDto
  {
    public int AxisId { get; set; }
    public string AxisName { get; set; } = string.Empty;
    public double? AxisLength { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ValueTypeKey { get; set; } = string.Empty;

    public int? RuleId { get; set; }

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
        Name = axis.Name,
        ValueTypeKey = axis.ValueTypeKey,
        RuleId = axis.RuleId,
        Entries = new List<Entry>()
      };

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

      var axis = new ProgesiAxisVariable(dto.AxisId, dto.AxisName, dto.Name, dto.ValueTypeKey, dto.AxisLength, dto.RuleId);

      if (dto.Entries != null)
      {
        foreach (var e in dto.Entries)
        {
          Guard.Against.Null(e, nameof(dto.Entries));
          if (double.IsNaN(e.Position) || double.IsInfinity(e.Position))
            throw new ArgumentOutOfRangeException(nameof(e.Position), "Position must be finite.");
          Guard.Against.Negative(e.VariableId, nameof(e.VariableId));

          // AddUnsafe: DTO contains ids only, signature validation happens at higher layer.
          axis.AddUnsafe(e.Position, e.VariableId, tol);
        }
      }

      return axis;
    }

    public IEnumerable<(int AxisId, double Position, int VariableId)> EnumerateFlat()
    {
      foreach (var e in Entries)
        yield return (AxisId, e.Position, e.VariableId);
    }
  }
}
