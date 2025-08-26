using Ardalis.GuardClauses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProgesiCore
{
    public sealed class ProgesiVariable : ValueObject
    {
        public int Id { get; }
        public string Name { get; }
        public int? MetadataId { get; } = null;
        public object Value { get; }
        public IReadOnlyList<int> DependsFrom { get; }

        public bool IsIndependent => DependsFrom == null || DependsFrom.Count == 0;

        public ProgesiVariable(
            int id,
            string name,
            object value,
            IReadOnlyList<int> dependsFrom = null,
            int? metadataId = null)
        {
            Guard.Against.NegativeOrZero(id, nameof(id));
            Guard.Against.NullOrWhiteSpace(name, nameof(name));
            Guard.Against.Null(value, nameof(value));
            Id = id;
            Name = name;
            Value = value;
            MetadataId = metadataId;
            DependsFrom = dependsFrom ?? Array.Empty<int>();
        }

        public override string ToString()
            => $"{Name} (ID: {Id}, Independent: {IsIndependent}, Value: {Value}, Metadata: {MetadataId})";

        public int CompareTo(ProgesiVariable other)
            => other is null ? 1 : Id.CompareTo(other.Id);

        public string GetObjectType(ProgesiVariable obj)
            => obj?.Value?.GetType().ToString() ?? "null";

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
            yield return Name;
            yield return MetadataId ?? 0;
            yield return Value;

            if (DependsFrom != null)
            {
                foreach (var d in DependsFrom)
                    yield return d;
            }
        }
    }
}
