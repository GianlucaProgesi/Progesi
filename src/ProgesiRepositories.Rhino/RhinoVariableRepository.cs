using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProgesiCore;
using Rhino;

namespace ProgesiRepositories.Rhino
{
    public sealed class RhinoVariableRepository : IVariableRepository
    {
        private readonly IRhinoDocAccessor _docAccessor;
        private const string Section = "ProgesiVariables";
        private const string IndexEntry = "Index";
        private const string IndexByHash = "IndexByHash";

        public RhinoVariableRepository(IRhinoDocAccessor docAccessor = null)
        {
            _docAccessor = docAccessor ?? new DefaultRhinoDocAccessor();
        }

        public Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default)
        {
            if (variable == null) throw new ArgumentNullException(nameof(variable));
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            // dedup per contenuto
            var hash = ProgesiHash.Compute(variable);
            var map = LoadIndexByHash(doc);
            if (map.TryGetValue(hash, out var existingId) && existingId != variable.Id)
            {
                // esiste identico ? non salvare
                return GetByIdAsync(existingId, ct);
            }

            var payload = Serialize(variable);
            doc.Strings.SetString(Section, EntryFor(variable.Id), payload);

            var ids = LoadIndex(doc);
            if (!ids.Contains(variable.Id))
                ids.Add(variable.Id);
            SaveIndex(doc, ids);

            map[hash] = variable.Id;
            SaveIndexByHash(doc, map);

            return Task.FromResult(variable);
        }

        public Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var payload = doc.Strings.GetValue(Section, EntryFor(id));
            if (string.IsNullOrWhiteSpace(payload))
                return Task.FromResult<ProgesiVariable>(null);

            var dto = JsonConvert.DeserializeObject<ProgesiVariableDto>(payload);
            if (dto is null)
                return Task.FromResult<ProgesiVariable>(null);

            var variable = Deserialize(dto);
            return Task.FromResult(variable);
        }

        public Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var ids = LoadIndex(doc);
            var list = new List<ProgesiVariable>();
            foreach (var id in ids)
            {
                var v = GetByIdAsync(id, ct).GetAwaiter().GetResult();
                if (!(v is null)) list.Add(v);
            }

            return Task.FromResult<IReadOnlyList<ProgesiVariable>>(list);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var existed = !string.IsNullOrWhiteSpace(doc.Strings.GetValue(Section, EntryFor(id)));
            if (existed)
            {
                doc.Strings.Delete(Section, EntryFor(id));

                var ids = LoadIndex(doc);
                if (ids.Remove(id)) SaveIndex(doc, ids);

                var map = LoadIndexByHash(doc).Where(kv => kv.Value != id)
                                              .ToDictionary(kv => kv.Key, kv => kv.Value);
                SaveIndexByHash(doc, map);
            }

            return Task.FromResult(existed);
        }

        public Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
        {
            if (idsToDelete == null) return Task.FromResult(0);

            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var ids = LoadIndex(doc);
            int count = 0;
            foreach (var id in idsToDelete)
            {
                var had = !string.IsNullOrWhiteSpace(doc.Strings.GetValue(Section, EntryFor(id)));
                if (had)
                {
                    doc.Strings.Delete(Section, EntryFor(id));
                    if (ids.Remove(id)) count++;
                }
            }
            SaveIndex(doc, ids);

            var map = LoadIndexByHash(doc);
            foreach (var kv in map.Where(kv => idsToDelete.Contains(kv.Value)).ToList())
                map.Remove(kv.Key);
            SaveIndexByHash(doc, map);

            return Task.FromResult(count);
        }

        private static string EntryFor(int id) => $"V:{id}";

        private static List<int> LoadIndex(RhinoDoc doc)
        {
            var json = doc.Strings.GetValue(Section, IndexEntry);
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();
            try { return JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>(); }
            catch { return new List<int>(); }
        }

        private static void SaveIndex(RhinoDoc doc, List<int> ids)
        {
            var json = JsonConvert.SerializeObject(ids.Distinct().OrderBy(x => x));
            doc.Strings.SetString(Section, IndexEntry, json);
        }

        private static Dictionary<string, int> LoadIndexByHash(RhinoDoc doc)
        {
            var json = doc.Strings.GetValue(Section, IndexByHash);
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                var map = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                return map ?? new Dictionary<string, int>(StringComparer.Ordinal);
            }
            catch { return new Dictionary<string, int>(StringComparer.Ordinal); }
        }

        private static void SaveIndexByHash(RhinoDoc doc, Dictionary<string, int> map)
        {
            var json = JsonConvert.SerializeObject(map);
            doc.Strings.SetString(Section, IndexByHash, json);
        }

        // -------- Serialization helpers --------

        private static string Serialize(ProgesiVariable v)
        {
            var dto = new ProgesiVariableDto
            {
                Id = v.Id,
                Name = v.Name ?? string.Empty,
                MetadataId = v.MetadataId,
                DependsFrom = (v.DependsFrom ?? Array.Empty<int>()).ToArray(),
                ValueType = TypeOf(v.Value),
                Value = Stringify(v.Value)
            };
            return JsonConvert.SerializeObject(dto);
        }

        private static ProgesiVariable Deserialize(ProgesiVariableDto dto)
        {
            var value = ParseValue(dto.Value, dto.ValueType);
            var depends = dto.DependsFrom ?? Array.Empty<int>();
            return new ProgesiVariable(dto.Id, dto.Name ?? string.Empty, value, depends, dto.MetadataId);
        }

        private static string TypeOf(object obj)
        {
            if (obj == null) return "null";
            switch (obj)
            {
                case string _: return "string";
                case int _: return "int";
                case double _: return "double";
                case bool _: return "bool";
                default: return obj.GetType().AssemblyQualifiedName;
            }
        }

        private static string Stringify(object obj)
        {
            if (obj == null) return "null";
            switch (obj)
            {
                case string s: return s;
                case int i: return i.ToString();
                case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                default: return JsonConvert.SerializeObject(obj);
            }
        }

        private static object ParseValue(string value, string valueType)
        {
            if (valueType == "null") return null;
            switch (valueType)
            {
                case "string": return value;
                case "int": return int.Parse(value);
                case "double": return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                case "bool": return value == "true";
                default:
                    var t = Type.GetType(valueType, throwOnError: false);
                    if (t == null) return value;
                    return JsonConvert.DeserializeObject(value, t);
            }
        }

        private sealed class ProgesiVariableDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int? MetadataId { get; set; }
            public int[] DependsFrom { get; set; } = Array.Empty<int>();
            public string ValueType { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
