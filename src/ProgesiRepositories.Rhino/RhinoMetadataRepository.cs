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
    public sealed class RhinoMetadataRepository : IMetadataRepository
    {
        private readonly IRhinoDocAccessor _docAccessor;
        private const string Section = "ProgesiMetadata";
        private const string IndexEntry = "Index";        // JSON array di Id
        private const string IndexByHash = "IndexByHash";  // JSON dict hash->Id

        public RhinoMetadataRepository(IRhinoDocAccessor docAccessor = null)
        {
            _docAccessor = docAccessor ?? new DefaultRhinoDocAccessor();
        }

        public Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var payload = doc.Strings.GetValue(Section, EntryFor(id));
            if (string.IsNullOrWhiteSpace(payload))
                return Task.FromResult<ProgesiMetadata?>(null);

            var dto = JsonConvert.DeserializeObject<ProgesiMetadataDto>(payload);
            if (dto is null)
                return Task.FromResult<ProgesiMetadata?>(null);

            var refs = (dto.References ?? new List<string>())
                       .Select(s => new Uri(s, UriKind.RelativeOrAbsolute));

            var meta = ProgesiMetadata.Create(
                dto.CreatedBy ?? string.Empty,
                dto.AdditionalInfo,
                refs,
                null,
                dto.LastModified,
                dto.Id);

            if (dto.Snips != null)
            {
                foreach (var s in dto.Snips)
                {
                    var bytes = s.Content ?? Array.Empty<byte>();
                    var mime = string.IsNullOrWhiteSpace(s.MimeType) ? "image/png" : s.MimeType;
                    var caption = s.Caption ?? string.Empty;
                    Uri? source = null;
                    if (!string.IsNullOrWhiteSpace(s.Source))
                        source = new Uri(s.Source, UriKind.RelativeOrAbsolute);

                    meta.AddSnip(bytes, mime, caption, source);
                }
            }

            return Task.FromResult<ProgesiMetadata?>(meta);
        }

        public Task UpsertAsync(ProgesiMetadata metadata, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var hash = ProgesiHash.Compute(metadata);

            // Dedup per hash
            var map = LoadIndexByHash(doc);
            if (map.TryGetValue(hash, out var existingId) && existingId != metadata.Id)
            {
                // esiste identico ? non salvare duplicato
                return Task.CompletedTask;
            }

            var dto = ProgesiMetadataDto.FromDomain(metadata);
            var payload = JsonConvert.SerializeObject(dto);
            doc.Strings.SetString(Section, EntryFor(metadata.Id), payload);

            // Aggiorna indici
            var ids = LoadIndex(doc);
            if (!ids.Contains(metadata.Id))
            {
                ids.Add(metadata.Id);
                SaveIndex(doc, ids);
            }
            map[hash] = metadata.Id;
            SaveIndexByHash(doc, map);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var existed = !string.IsNullOrWhiteSpace(doc.Strings.GetValue(Section, EntryFor(id)));
            if (existed)
            {
                // togli voce e ripulisci hash corrispondente (ricalcolo semplice: rimuovi tutte le mappature che puntano a id)
                doc.Strings.Delete(Section, EntryFor(id));

                var ids = LoadIndex(doc);
                if (ids.Remove(id)) SaveIndex(doc, ids);

                var map = LoadIndexByHash(doc).Where(kv => kv.Value != id)
                                              .ToDictionary(kv => kv.Key, kv => kv.Value);
                SaveIndexByHash(doc, map);
            }

            return Task.FromResult(existed);
        }

        public Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default)
        {
            var doc = _docAccessor.GetActiveDoc();
            if (doc == null) throw new InvalidOperationException("No active Rhino document.");

            var ids = LoadIndex(doc).Skip(skip).Take(take).ToList();
            var list = new List<ProgesiMetadata>();
            foreach (var id in ids)
            {
                var m = GetAsync(id, ct).GetAwaiter().GetResult();
                if (!(m is null)) list.Add(m); // C# 8
            }
            return Task.FromResult<IReadOnlyList<ProgesiMetadata>>(list);
        }

        private static string EntryFor(int id) => $"M:{id}";

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

        // --- DTO ---

        private sealed class ProgesiMetadataDto
        {
            public int Id { get; set; }
            public DateTime LastModified { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public string AdditionalInfo { get; set; } = string.Empty;
            public List<string> References { get; set; } = new List<string>();
            public List<SnipDto> Snips { get; set; } = new List<SnipDto>();

            public static ProgesiMetadataDto FromDomain(ProgesiMetadata m)
            {
                return new ProgesiMetadataDto
                {
                    Id = m.Id,
                    LastModified = m.LastModified,
                    CreatedBy = m.CreatedBy,
                    AdditionalInfo = m.AdditionalInfo,
                    References = m.References?.Select(u => u.ToString()).ToList() ?? new List<string>(),
                    Snips = m.Snips?.Select(s => new SnipDto
                    {
                        Id = s.Id,
                        MimeType = s.MimeType,
                        Caption = s.Caption,
                        Source = s.Source,
                        Content = s.Content
                    }).ToList() ?? new List<SnipDto>()
                };
            }
        }

        private sealed class SnipDto
        {
            public Guid Id { get; set; }
            public string MimeType { get; set; } = string.Empty;
            public string Caption { get; set; } = string.Empty;
            public string? Source { get; set; }
            public byte[] Content { get; set; } = Array.Empty<byte>();
        }
    }
}
