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
  public sealed class RhinoMetadataRepository : IMetadataRepository
  {
    private readonly StringTable _table;

    public RhinoMetadataRepository(RhinoDoc doc)
    {
      if (doc is null) throw new ArgumentNullException(nameof(doc));
      _table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
    }

    public Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
    {
      var key = KeyOf(id);
      // FIX: GetValue (non GetString)
      var json = _table.GetValue("Progesi.Meta", key);
      if (string.IsNullOrWhiteSpace(json))
        return Task.FromResult<ProgesiMetadata?>(null);

      var dto = JsonConvert.DeserializeObject<Dto>(json!);
      if (dto == null)
        return Task.FromResult<ProgesiMetadata?>(null);

      var refs = new List<Uri>();
      if (dto.References != null)
      {
        foreach (var s in dto.References)
        {
          if (!string.IsNullOrWhiteSpace(s) && Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u))
            refs.Add(u);
        }
      }

      var meta = ProgesiMetadata.Create(dto.CreatedBy ?? string.Empty,
                                        dto.AdditionalInfo ?? string.Empty,
                                        refs,
                                        lastModifiedUtc: dto.LastModified,
                                        id: dto.Id);

      if (dto.Snips != null)
      {
        foreach (var sn in dto.Snips)
        {
          Uri? src = null;
          if (!string.IsNullOrWhiteSpace(sn.Source) && Uri.TryCreate(sn.Source, UriKind.RelativeOrAbsolute, out var u))
            src = u;

          if (src is null)
            meta.AddSnip(sn.Content ?? Array.Empty<byte>(), sn.MimeType ?? "image/png", sn.Caption ?? string.Empty);
          else
            meta.AddSnip(sn.Content ?? Array.Empty<byte>(), sn.MimeType ?? "image/png", sn.Caption ?? string.Empty, src);
        }
      }

      return Task.FromResult<ProgesiMetadata?>(meta);
    }

    public Task UpsertAsync(ProgesiMetadata meta, CancellationToken ct = default)
    {
      var key = KeyOf(meta.Id);

      var dto = new Dto
      {
        Id = meta.Id,
        LastModified = meta.LastModified,
        CreatedBy = meta.CreatedBy ?? string.Empty,
        AdditionalInfo = meta.AdditionalInfo ?? string.Empty,
        References = meta.References is null ? Array.Empty<string>() : Array.ConvertAll(meta.References.ToArray(), u => u.ToString()),
        Snips = meta.Snips is null ? Array.Empty<SnipDto>() : Array.ConvertAll(meta.Snips.ToArray(), s => new SnipDto
        {
          MimeType = s.MimeType ?? "image/png",
          Caption = s.Caption ?? string.Empty,
          Source = s.Source?.ToString(),
          Content = s.Content ?? Array.Empty<byte>()
        })
      };

      var json = JsonConvert.SerializeObject(dto) ?? string.Empty;
      _table.SetString("Progesi.Meta", key, json); // ? niente var = ...
      return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      var key = KeyOf(id);
      _table.Delete("Progesi.Meta", key);
      return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default)
    {
      IReadOnlyList<ProgesiMetadata> empty = Array.Empty<ProgesiMetadata>();
      return Task.FromResult(empty);
    }

    private static string KeyOf(int id) => $"meta:{id}";

    private sealed class Dto
    {
      public int Id { get; set; }
      public DateTime LastModified { get; set; }
      public string? CreatedBy { get; set; }
      public string? AdditionalInfo { get; set; }
      public string[]? References { get; set; }
      public SnipDto[]? Snips { get; set; }
    }

    private sealed class SnipDto
    {
      public string? MimeType { get; set; }
      public string? Caption { get; set; }
      public string? Source { get; set; }
      public byte[]? Content { get; set; }
    }
  }
}
