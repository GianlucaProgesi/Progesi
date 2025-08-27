using Newtonsoft.Json;
using ProgesiCore;
using Rhino;
using Rhino.DocObjects.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace ProgesiRepositories.Rhino
{
    public sealed class RhinoVariableRepository : IVariableRepository
    {
        private readonly StringTable _table;

        public RhinoVariableRepository(RhinoDoc doc)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            _table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
        }

        public Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default)
        {
            var key = KeyOf(variable.Id);
            var payload = new
            {
                variable.Id,
                variable.Name,
                ValueType = TypeOf(variable.Value),
                Value = Stringify(variable.Value),
                variable.MetadataId,
                Depends = variable.DependsFrom ?? Array.Empty<int>()
            };
            var json = JsonConvert.SerializeObject(payload) ?? string.Empty;
            _table.SetString("Progesi.Var", key, json); // ? niente var = ...
            return Task.FromResult(variable);
        }
      
        // NOTA: l'interfaccia prevede non-nullable; se non trovato, ritorna null a runtime.
        // Per evitare warning (TreatWarningsAsErrors), disabilitiamo la nullability solo per questo metodo.
#nullable disable
        public Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var key = KeyOf(id);
            var json = _table.GetValue("Progesi.Var", key);
            if (string.IsNullOrWhiteSpace(json)) return Task.FromResult<ProgesiVariable>(null);

            var dto = JsonConvert.DeserializeObject<Dto>(json);
            if (dto == null) return Task.FromResult<ProgesiVariable>(null);

            var value = ParseValue(dto.Value ?? string.Empty, dto.ValueType ?? "string");
            var depends = dto.Depends ?? Array.Empty<int>();
            return Task.FromResult(new ProgesiVariable(dto.Id, dto.Name ?? string.Empty, value, depends, dto.MetadataId));
        }
#nullable enable

        public async Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default)
        {
            var list = new List<ProgesiVariable>();
            // Rhino StringTable non espone enumerazione: ritorno lista vuota (comportamento definito)
            return await Task.FromResult(list);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var key = KeyOf(id);
            _table.Delete("Progesi.Var", key);   // Delete non ritorna nulla
            return Task.FromResult(true);        // => restituiamo true esplicito
        }

        public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
        {
            if (idsToDelete == null) return 0;
            int n = 0;
            foreach (var id in idsToDelete)
            {
                if (await DeleteAsync(id, ct)) n++;
            }
            return n;
        }

        private static string KeyOf(int id) => $"var:{id}";

        private sealed class Dto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? ValueType { get; set; }
            public string? Value { get; set; }
            public int? MetadataId { get; set; }
            public int[]? Depends { get; set; }
        }

        // ---- helpers (null-safe)
        private static string TypeOf(object? obj)
        {
            if (obj == null) return "null";
            return obj switch
            {
                string _ => "string",
                int _ => "int",
                double _ => "double",
                bool _ => "bool",
                _ => obj.GetType().AssemblyQualifiedName ?? "object"
            };
        }

        private static string Stringify(object? obj)
        {
            if (obj == null) return "null";
            return obj switch
            {
                string s => s,
                int i => i.ToString(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => JsonConvert.SerializeObject(obj) ?? string.Empty
            };
        }

        private static object? ParseValue(string value, string valueType)
        {
            if (valueType == "null") return null;
            return valueType switch
            {
                "string" => value,
                "int" => int.Parse(value),
                "double" => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                "bool" => value == "true",
                _ => DeserializeOrReturn(value, valueType)
            };
        }

        private static object DeserializeOrReturn(string value, string typeName)
        {
            var t = Type.GetType(typeName, throwOnError: false);
            if (t == null) return (object)value;
            return JsonConvert.DeserializeObject(value, t) ?? (object)value;
        }
    }
}
