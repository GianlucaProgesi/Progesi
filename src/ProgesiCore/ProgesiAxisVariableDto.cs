using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace ProgesiCore.Serialization
{
    /// <summary>
    /// DTO piatto per serializzare/deserializzare ProgesiAxisVariable.
    /// Pensato per tabelle tipo:
    ///   Axis:      (AxisId, AxisName, AxisLength, RuleId)
    ///   AxisEntry: (AxisId, VariableName, Position, VariableId)
    /// </summary>
    public sealed class ProgesiAxisVariableDto
    {
        public int AxisId { get; set; }
        public string AxisName { get; set; } = string.Empty;
        public double? AxisLength { get; set; }
        public int? RuleId { get; set; }

        public List<Entry> Entries { get; set; } = new List<Entry>();

        public sealed class Entry
        {
            public string VariableName { get; set; } = string.Empty;
            public double Position { get; set; }
            public int VariableId { get; set; }
        }

        /// <summary>Crea un DTO a partire dal dominio.</summary>
        public static ProgesiAxisVariableDto FromDomain(ProgesiAxisVariable axis)
        {
            Guard.Against.Null(axis, nameof(axis));

            var dto = new ProgesiAxisVariableDto
            {
                AxisId = axis.Id,
                AxisName = axis.AxisName,
                AxisLength = axis.AxisLength,
                RuleId = axis.RuleId,
                Entries = new List<Entry>()
            };

            foreach (var t in axis.EnumerateAll())
            {
                dto.Entries.Add(new Entry
                {
                    VariableName = t.variableName,
                    Position = t.position,
                    VariableId = t.variableId
                });
            }

            return dto;
        }

        /// <summary>
        /// Ricostruisce il dominio dal DTO. Valida input come farebbe il dominio (guard clauses + vincoli AxisLength).
        /// </summary>
        public static ProgesiAxisVariable ToDomain(ProgesiAxisVariableDto dto, double tol = ProgesiAxisVariable.DefaultTolerance)
        {
            Guard.Against.Null(dto, nameof(dto));
            Guard.Against.Negative(dto.AxisId, nameof(dto.AxisId));
            Guard.Against.NullOrWhiteSpace(dto.AxisName, nameof(dto.AxisName));
            if (dto.AxisLength.HasValue) Guard.Against.NegativeOrZero(dto.AxisLength.Value, nameof(dto.AxisLength));
            if (dto.RuleId.HasValue) Guard.Against.Negative(dto.RuleId.Value, nameof(dto.RuleId));

            var axis = new ProgesiAxisVariable(dto.AxisId, dto.AxisName, dto.AxisLength, dto.RuleId);

            if (dto.Entries != null)
            {
                foreach (var e in dto.Entries)
                {
                    Guard.Against.Null(e, nameof(dto.Entries));
                    Guard.Against.NullOrWhiteSpace(e.VariableName, nameof(e.VariableName));
                    if (double.IsNaN(e.Position) || double.IsInfinity(e.Position))
                        throw new ArgumentOutOfRangeException(nameof(e.Position), "Position must be finite.");
                    Guard.Against.Negative(e.VariableId, nameof(e.VariableId));

                    axis.Add(e.VariableName, e.Position, e.VariableId, tol);
                }
            }

            return axis;
        }

        /// <summary>
        /// Helper: crea una sequenza piatta ordinabile per export (es. CSV/SQL bulk insert).
        /// </summary>
        public IEnumerable<(int AxisId, string VariableName, double Position, int VariableId)> EnumerateFlat()
        {
            foreach (var e in Entries)
            {
                yield return (AxisId, e.VariableName, e.Position, e.VariableId);
            }
        }
    }
}
